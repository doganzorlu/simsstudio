using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using SimsConverter.Domain.Contracts;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Services;

public class Ts3MlodGeometryDecoder : ITs3MlodGeometryDecoder
{
    private const uint VfrtMagic = 0x54524656u; // "VFRT" in Little Endian (0x56, 0x52, 0x54, 0x46 -> 0x54524656)
    private const uint VbufMagic = 0x46554256u; // "VBUF" in Little Endian (0x56, 0x42, 0x55, 0x46 -> 0x46554256)
    private const uint IbufMagic = 0x46554249u; // "IBUF" in Little Endian (0x49, 0x42, 0x55, 0x46 -> 0x46554249)

    private readonly ICanonicalMeshValidator _meshValidator;

    public Ts3MlodGeometryDecoder(ICanonicalMeshValidator? meshValidator = null)
    {
        _meshValidator = meshValidator ?? new CanonicalMeshValidator();
    }

    public Ts3GeomImportResult Decode(ReadOnlySpan<byte> buffer, string? nameHint = null)
    {
        var issues = new List<ConversionIssue>();

        if (buffer.IsEmpty || buffer.Length < 32)
        {
            issues.Add(new ConversionIssue("MLOD001", "Payload buffer is empty or too small for MLOD geometry decoding.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        int vfrtPos = -1;
        int vbufPos = -1;
        int ibufPos = -1;

        for (int i = 0; i <= buffer.Length - 4; i++)
        {
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(i, 4));
            if ((magic == 0x54524656u || magic == 0x46545256u) && vfrtPos < 0)
            {
                vfrtPos = i;
            }
            else if (magic == VbufMagic && vbufPos < 0) vbufPos = i;
            else if (magic == IbufMagic && ibufPos < 0) ibufPos = i;
        }

        // Strict Requirement: VFRT, VBUF, and IBUF stream markers must all be present
        if (vfrtPos < 0)
        {
            issues.Add(new ConversionIssue("CONVG007", "Required VFRT (Vertex Format) stream marker not found in MLOD payload.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        if (vbufPos < 0 || ibufPos < 0)
        {
            issues.Add(new ConversionIssue("CONVG007", "No VBUF/IBUF mesh stream markers found in MLOD payload.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        if (vfrtPos + 16 > buffer.Length)
        {
            issues.Add(new ConversionIssue("MLOD002", "Truncated VFRT chunk header.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        uint vfrtVersion = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(vfrtPos + 4, 4));
        uint parsedStride = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(vfrtPos + 8, 4));
        uint elemCount = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(vfrtPos + 12, 4));

        if (parsedStride == 0 || parsedStride > 256)
        {
            issues.Add(new ConversionIssue("MLOD003", $"Invalid VFRT vertex stride: {parsedStride} bytes.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        int stride = (int)parsedStride;
        int? posOffset = null;
        int posFormat = 1;
        int? normalOffset = null;
        int? uv0Offset = null;
        int? boneIdxOffset = null;
        int? boneWgtOffset = null;

        int currElem = vfrtPos + 16;
        for (uint e = 0; e < elemCount; e++)
        {
            if (currElem + 8 > buffer.Length)
            {
                issues.Add(new ConversionIssue("MLOD002", "Truncated VFRT element descriptors array.", ConversionIssueSeverity.Error));
                return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
            }

            ushort streamIdx = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(currElem, 2));
            ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(currElem + 2, 2));
            ushort format = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(currElem + 4, 2));
            ushort usage = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(currElem + 6, 2));

            // Log parsed element descriptor values
            System.Diagnostics.Debug.WriteLine($"[VFRT ELEM {e}] stream={streamIdx}, offset={offset}, format={format}, usage={usage}");

            if (usage == 1 || usage == 0 || format == 7 || format == 1) // Position candidate
            {
                if (!posOffset.HasValue || usage == 1)
                {
                    posOffset = offset;
                    posFormat = format;
                }
            }
            if (usage == 2 || usage == 1) // Normal candidate
            {
                if (!normalOffset.HasValue || usage == 2) normalOffset = offset;
            }
            if ((usage == 3 || usage == 2) && !uv0Offset.HasValue) // UV0 candidate
            {
                uv0Offset = offset;
            }
            if (usage == 4 || usage == 3) // Bone Indices
            {
                boneIdxOffset = offset;
            }
            if (usage == 5 || usage == 4) // Bone Weights
            {
                boneWgtOffset = offset;
            }

            currElem += 8;
        }

        if (!posOffset.HasValue)
        {
            issues.Add(new ConversionIssue("CONVG007", "VFRT chunk does not contain a required Position attribute descriptor.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        // Parse Vertex Buffer
        int vDataStart = vbufPos + 16;
        int vDataLen = (ibufPos > vbufPos) ? (ibufPos - vDataStart) : (buffer.Length - vDataStart);

        if (vDataLen < stride || vDataLen <= 0)
        {
            issues.Add(new ConversionIssue("MLOD002", $"Truncated VBUF section: length {vDataLen} bytes is smaller than vertex stride {stride} bytes.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        int vertexCount = vDataLen / stride;
        var vertices = new List<CanonicalVertex>();

        for (int v = 0; v < vertexCount; v++)
        {
            int vOff = vDataStart + v * stride;
            if (vOff + stride > buffer.Length)
            {
                issues.Add(new ConversionIssue("MLOD002", "Truncated VBUF vertex data buffer.", ConversionIssueSeverity.Error));
                return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
            }

            var vSpan = buffer.Slice(vOff, stride);

            // Decode Position
            MeshVector3 position;
            int pOff = posOffset.Value;
            if (posFormat == 7 && pOff + 12 <= vSpan.Length)
            {
                float px = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(pOff, 4));
                float py = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(pOff + 4, 4));
                float pz = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(pOff + 8, 4));
                position = new MeshVector3(px, py, pz);
            }
            else if (pOff + 6 <= vSpan.Length)
            {
                short sx = BinaryPrimitives.ReadInt16LittleEndian(vSpan.Slice(pOff, 2));
                short sy = BinaryPrimitives.ReadInt16LittleEndian(vSpan.Slice(pOff + 2, 2));
                short sz = BinaryPrimitives.ReadInt16LittleEndian(vSpan.Slice(pOff + 4, 2));
                position = new MeshVector3(sx / 32767.0f, sy / 32767.0f, sz / 32767.0f);
            }
            else
            {
                issues.Add(new ConversionIssue("MLOD002", "Truncated vertex position data inside vertex buffer.", ConversionIssueSeverity.Error));
                return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
            }

            // Decode Normal
            MeshVector3 normal = new MeshVector3(0, 1, 0);
            if (normalOffset.HasValue && normalOffset.Value + 3 <= vSpan.Length)
            {
                int nOff = normalOffset.Value;
                sbyte nx = (sbyte)vSpan[nOff];
                sbyte ny = (sbyte)vSpan[nOff + 1];
                sbyte nz = (sbyte)vSpan[nOff + 2];
                normal = new MeshVector3(nx / 127.0f, ny / 127.0f, nz / 127.0f);
            }

            // Decode UV0
            MeshVector2 uv0 = new MeshVector2(0, 0);
            if (uv0Offset.HasValue && uv0Offset.Value + 4 <= vSpan.Length)
            {
                int uOff = uv0Offset.Value;
                short uRaw = BinaryPrimitives.ReadInt16LittleEndian(vSpan.Slice(uOff, 2));
                short vRaw = BinaryPrimitives.ReadInt16LittleEndian(vSpan.Slice(uOff + 2, 2));
                uv0 = new MeshVector2(uRaw / 32767.0f, vRaw / 32767.0f);
            }

            // Decode Bone Weights
            var boneWeights = new List<CanonicalBoneWeight>();
            if (boneIdxOffset.HasValue && boneWgtOffset.HasValue &&
                boneIdxOffset.Value < vSpan.Length && boneWgtOffset.Value < vSpan.Length)
            {
                byte bIndex = vSpan[boneIdxOffset.Value];
                byte bWeight = vSpan[boneWgtOffset.Value];
                float wFloat = bWeight / 255.0f;
                if (wFloat > 0.001f)
                {
                    boneWeights.Add(new CanonicalBoneWeight(bIndex, wFloat));
                }
            }

            if (boneWeights.Count == 0)
            {
                boneWeights.Add(new CanonicalBoneWeight(0, 1.0f));
            }

            vertices.Add(new CanonicalVertex(
                position: position,
                normal: normal,
                tangent: null,
                uv0: uv0,
                uv1: null,
                boneWeights: boneWeights.AsReadOnly()
            ));
        }

        // Parse Index Buffer
        int iDataStart = ibufPos + 16;
        int iDataLen = buffer.Length - iDataStart;

        for (int k = iDataStart; k <= buffer.Length - 4; k += 4)
        {
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(k, 4));
            if (magic == VfrtMagic || magic == VbufMagic || magic == IbufMagic)
            {
                iDataLen = k - iDataStart;
                break;
            }
        }

        if (iDataLen < 6)
        {
            issues.Add(new ConversionIssue("MLOD002", $"Truncated IBUF section: length {iDataLen} bytes is too small.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        int indexCount = iDataLen / 2;
        int faceCount = indexCount / 3;
        var faces = new List<CanonicalFace>();

        for (int f = 0; f < faceCount; f++)
        {
            int fOff = iDataStart + f * 6;
            if (fOff + 6 > buffer.Length) break;

            ushort idxA = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(fOff, 2));
            ushort idxB = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(fOff + 2, 2));
            ushort idxC = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(fOff + 4, 2));

            if (idxA < vertexCount && idxB < vertexCount && idxC < vertexCount)
            {
                faces.Add(new CanonicalFace(idxA, idxB, idxC));
            }
        }

        if (vertices.Count < 3 || faces.Count < 1)
        {
            issues.Add(new ConversionIssue("CONVG007", "Extracted MODL/MLOD geometry stream yielded insufficient valid vertices or faces.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        string meshName = !string.IsNullOrWhiteSpace(nameHint) ? nameHint : "TS3_MLOD_Mesh";
        var materials = new[] { new CanonicalMaterialSlot(0, "DefaultMaterial") };

        var canonicalMesh = new CanonicalMesh(
            name: meshName,
            vertices: vertices,
            faces: faces,
            materials: materials,
            coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
            sourceGameVersion: GameVersion.Sims3,
            issues: issues
        );

        var validationResult = _meshValidator.Validate(canonicalMesh);
        var finalIssues = issues.Concat(validationResult.Issues).ToList();

        bool overallSuccess = validationResult.IsSuccess && !finalIssues.Any(i => i.Severity == ConversionIssueSeverity.Error || i.Severity == ConversionIssueSeverity.Fatal);

        return new Ts3GeomImportResult(
            IsSuccess: overallSuccess,
            Mesh: canonicalMesh,
            Issues: finalIssues.AsReadOnly()
        );
    }
}
