using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Contracts;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Services;

public class Ts3GeomCanonicalMeshImporter : ITs3GeomCanonicalMeshImporter
{
    private const uint GeomMagicLe = 0x4D4F4547;
    private const uint MtnfMagicLe = 0x464E544D;

    private readonly ITs3GeomMetadataReader _metadataReader;
    private readonly ICanonicalMeshValidator _meshValidator;

    public Ts3GeomCanonicalMeshImporter(
        ITs3GeomMetadataReader metadataReader,
        ICanonicalMeshValidator meshValidator)
    {
        _metadataReader = metadataReader ?? throw new ArgumentNullException(nameof(metadataReader));
        _meshValidator = meshValidator ?? throw new ArgumentNullException(nameof(meshValidator));
    }

    public Ts3GeomCanonicalMeshImporter()
        : this(new Ts3GeomMetadataReader(), new CanonicalMeshValidator())
    {
    }

    private readonly struct VertexElementDescriptor
    {
        public uint DataType { get; }
        public uint Format { get; }
        public byte SizeBytes { get; }
        public int OffsetInVertex { get; }

        public VertexElementDescriptor(uint dataType, uint format, byte sizeBytes, int offsetInVertex)
        {
            DataType = dataType;
            Format = format;
            SizeBytes = sizeBytes;
            OffsetInVertex = offsetInVertex;
        }
    }

    public Ts3GeomImportResult Import(ReadOnlySpan<byte> buffer, string? nameHint = null)
    {
        var issues = new List<ConversionIssue>();

        if (buffer.IsEmpty)
        {
            issues.Add(new ConversionIssue("MESHG000", "GEOM payload buffer is empty.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        // 1. Run Structural Metadata Reader (Candidate Layout Validation)
        var metaResult = _metadataReader.Read(buffer);
        issues.AddRange(metaResult.Issues);

        if (!metaResult.IsSuccess || metaResult.Metadata == null)
        {
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        var metadata = metaResult.Metadata;

        // 2. Strict Chunk Slice Selection from Verified Metadata
        int chunkOffset = metadata.GeomChunkOffset;
        int effectiveChunkSize = metadata.GeomChunkSize > 0 ? (int)metadata.GeomChunkSize : (buffer.Length - chunkOffset);

        if (chunkOffset < 0 || chunkOffset + effectiveChunkSize > buffer.Length || effectiveChunkSize < 20)
        {
            issues.Add(new ConversionIssue("MESHG001", $"Invalid or out-of-bounds GEOM chunk slice at offset {chunkOffset} (size {effectiveChunkSize}).", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        ReadOnlySpan<byte> chunkSpan = buffer.Slice(chunkOffset, effectiveChunkSize);

        // 3. Parse GEOM Chunk Header
        int curr = 0;
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        uint geomVersion = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        uint rawTgiOffset = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        uint tgiSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        ulong shaderId = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;

        // MTNF / Zero Padding Skip
        if (curr + 4 <= chunkSpan.Length)
        {
            uint firstWord = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));

            if (curr + 8 <= chunkSpan.Length)
            {
                uint secondWord = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr + 4, 4));
                if (secondWord == MtnfMagicLe)
                {
                    curr += 8 + (int)firstWord;
                }
                else if (firstWord == MtnfMagicLe)
                {
                    curr += 8 + (int)secondWord;
                }
                else if (firstWord == 0 && shaderId == 0)
                {
                    curr += 4;
                }
            }
            else if (firstWord == 0 && shaderId == 0)
            {
                curr += 4;
            }
        }

        if (curr + 16 > chunkSpan.Length)
        {
            issues.Add(new ConversionIssue("MESHG002", "Truncated GEOM vertex metadata fields.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        int mergeGroup = BinaryPrimitives.ReadInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        int sortOrder = BinaryPrimitives.ReadInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        uint vertexCount = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        uint vertexElementCount = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;

        // 4. Parse Vertex Element Descriptors (9 bytes per descriptor)
        var descriptors = new List<VertexElementDescriptor>();
        int currentElementOffset = 0;

        if (curr + (int)vertexElementCount * 9 > chunkSpan.Length)
        {
            issues.Add(new ConversionIssue("MESHG002", "Truncated 9-byte vertex element descriptors section.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        bool hasPosition = false;

        for (uint e = 0; e < vertexElementCount; e++)
        {
            int descriptorOffset = curr + (int)e * 9;
            uint dataType = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(descriptorOffset, 4));
            uint format = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(descriptorOffset + 4, 4));
            byte sizeBytes = chunkSpan[descriptorOffset + 8];

            if (sizeBytes == 0)
            {
                sizeBytes = GetDefaultFormatSizeBytes(format);
            }

            descriptors.Add(new VertexElementDescriptor(dataType, format, sizeBytes, currentElementOffset));
            currentElementOffset += sizeBytes;

            if (dataType == 1)
            {
                hasPosition = true;
            }
            else if (dataType != 2 && dataType != 3 && dataType != 4 && dataType != 5 && dataType != 6 && dataType != 7 && dataType != 10)
            {
                issues.Add(new ConversionIssue(
                    "MESHG003",
                    $"Unrecognized vertex descriptor datatype: {dataType} (Format: {format}, Size: {sizeBytes}B).",
                    ConversionIssueSeverity.Warning
                ));
            }
        }
        curr += (int)vertexElementCount * 9;

        int vertexStrideBytes = currentElementOffset > 0 ? currentElementOffset : (int)metadata.VertexStrideBytes;

        if (!hasPosition)
        {
            issues.Add(new ConversionIssue("MESHG004", "GEOM vertex element descriptors do not contain a required Position (datatype 1) element.", ConversionIssueSeverity.Warning));
        }

        // 5. Decode Vertex Buffer
        long totalVertexBufferBytes = (long)vertexCount * vertexStrideBytes;
        if (curr + totalVertexBufferBytes > chunkSpan.Length)
        {
            issues.Add(new ConversionIssue("MESHG002", $"Truncated GEOM vertex buffer: expected {totalVertexBufferBytes} bytes at offset {curr}, actual chunk length {chunkSpan.Length}.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        var vertices = new List<CanonicalVertex>();
        var positionDesc = descriptors.FirstOrDefault(d => d.DataType == 1);
        var normalDesc = descriptors.FirstOrDefault(d => d.DataType == 2);
        var uv0Desc = descriptors.Where(d => d.DataType == 3).FirstOrDefault();
        var uv1Desc = descriptors.Where(d => d.DataType == 3).Skip(1).FirstOrDefault();
        var boneIdxDesc = descriptors.FirstOrDefault(d => d.DataType == 4);
        var boneWgtDesc = descriptors.FirstOrDefault(d => d.DataType == 5);
        var tangentDesc = descriptors.FirstOrDefault(d => d.DataType == 6);

        for (uint i = 0; i < vertexCount; i++)
        {
            int vOffset = curr + (int)i * vertexStrideBytes;
            var vSpan = chunkSpan.Slice(vOffset, vertexStrideBytes);

            // Decode Position (DataType 1: 3 x Float)
            MeshVector3 position = default;
            if (hasPosition && positionDesc.OffsetInVertex + 12 <= vSpan.Length)
            {
                float x = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(positionDesc.OffsetInVertex, 4));
                float y = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(positionDesc.OffsetInVertex + 4, 4));
                float z = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(positionDesc.OffsetInVertex + 8, 4));
                position = new MeshVector3(x, y, z);
            }

            // Decode Normal (DataType 2: 3 x Float)
            MeshVector3? normal = null;
            if (normalDesc.SizeBytes >= 12 && normalDesc.OffsetInVertex + 12 <= vSpan.Length)
            {
                float nx = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(normalDesc.OffsetInVertex, 4));
                float ny = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(normalDesc.OffsetInVertex + 4, 4));
                float nz = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(normalDesc.OffsetInVertex + 8, 4));
                normal = new MeshVector3(nx, ny, nz);
            }

            // Decode Tangent (DataType 6: 3 or 4 x Float)
            MeshVector4? tangent = null;
            if (tangentDesc.SizeBytes >= 12 && tangentDesc.OffsetInVertex + 12 <= vSpan.Length)
            {
                float tx = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(tangentDesc.OffsetInVertex, 4));
                float ty = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(tangentDesc.OffsetInVertex + 4, 4));
                float tz = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(tangentDesc.OffsetInVertex + 8, 4));
                float tw = tangentDesc.SizeBytes >= 16 ? BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(tangentDesc.OffsetInVertex + 12, 4)) : 1.0f;
                tangent = new MeshVector4(tx, ty, tz, tw);
            }

            // Decode UV0 (DataType 3: 2 x Float)
            MeshVector2? uv0 = null;
            if (uv0Desc.SizeBytes >= 8 && uv0Desc.OffsetInVertex + 8 <= vSpan.Length)
            {
                float u0 = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(uv0Desc.OffsetInVertex, 4));
                float v0 = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(uv0Desc.OffsetInVertex + 4, 4));
                uv0 = new MeshVector2(u0, v0);
            }

            // Decode UV1 (DataType 3 second occurrence: 2 x Float)
            MeshVector2? uv1 = null;
            if (uv1Desc.SizeBytes >= 8 && uv1Desc.OffsetInVertex + 8 <= vSpan.Length)
            {
                float u1 = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(uv1Desc.OffsetInVertex, 4));
                float v1 = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(uv1Desc.OffsetInVertex + 4, 4));
                uv1 = new MeshVector2(u1, v1);
            }

            // Decode Bone Weights (DataType 4: 4 x Byte, DataType 5: 4 x Float)
            List<CanonicalBoneWeight>? boneWeights = null;
            if (boneIdxDesc.SizeBytes >= 4 && boneWgtDesc.SizeBytes >= 12 &&
                boneIdxDesc.OffsetInVertex + 4 <= vSpan.Length && boneWgtDesc.OffsetInVertex + 12 <= vSpan.Length)
            {
                boneWeights = new List<CanonicalBoneWeight>();
                int weightFloatCount = boneWgtDesc.SizeBytes >= 16 ? 4 : 3;

                for (int b = 0; b < weightFloatCount; b++)
                {
                    byte bIndex = vSpan[boneIdxDesc.OffsetInVertex + b];
                    float bWeight = BinaryPrimitives.ReadSingleLittleEndian(vSpan.Slice(boneWgtDesc.OffsetInVertex + b * 4, 4));
                    if (bWeight > 0.0001f)
                    {
                        boneWeights.Add(new CanonicalBoneWeight(bIndex, bWeight));
                    }
                }
            }

            vertices.Add(new CanonicalVertex(position, normal, tangent, uv0, uv1, boneWeights));
        }
        curr += (int)totalVertexBufferBytes;

        // 6. Parse Face Group / Index Buffer (geom_write.py lines 100-120: groupMarker 4B + faceFormat 1B + facePointCount 4B)
        if (curr + 9 > chunkSpan.Length)
        {
            issues.Add(new ConversionIssue("MESHG002", "Truncated GEOM face group header.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        uint groupMarker = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;
        byte faceFormat = chunkSpan[curr];
        curr += 1;
        uint indexCount = BinaryPrimitives.ReadUInt32LittleEndian(chunkSpan.Slice(curr, 4));
        curr += 4;

        long totalIndexBufferBytes = (long)indexCount * 2;
        if (curr + totalIndexBufferBytes > chunkSpan.Length)
        {
            issues.Add(new ConversionIssue("MESHG002", $"Truncated GEOM index buffer: expected {totalIndexBufferBytes} bytes at offset {curr}, actual chunk length {chunkSpan.Length}.", ConversionIssueSeverity.Error));
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues.AsReadOnly());
        }

        var faces = new List<CanonicalFace>();
        uint faceCount = indexCount / 3;

        for (uint f = 0; f < faceCount; f++)
        {
            int fOffset = curr + (int)f * 6;
            ushort idxA = BinaryPrimitives.ReadUInt16LittleEndian(chunkSpan.Slice(fOffset, 2));
            ushort idxB = BinaryPrimitives.ReadUInt16LittleEndian(chunkSpan.Slice(fOffset + 2, 2));
            ushort idxC = BinaryPrimitives.ReadUInt16LittleEndian(chunkSpan.Slice(fOffset + 4, 2));

            faces.Add(new CanonicalFace(idxA, idxB, idxC));
        }

        // 7. Construct CanonicalMesh
        string meshName = !string.IsNullOrWhiteSpace(nameHint) ? nameHint : "TS3_GEOM_Mesh";
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

        // 8. Validate CanonicalMesh with ICanonicalMeshValidator
        var validationResult = _meshValidator.Validate(canonicalMesh);
        var finalIssues = issues.Concat(validationResult.Issues).ToList();

        bool overallSuccess = validationResult.IsSuccess && !finalIssues.Any(i => i.Severity == ConversionIssueSeverity.Error || i.Severity == ConversionIssueSeverity.Fatal);

        return new Ts3GeomImportResult(
            IsSuccess: overallSuccess,
            Mesh: canonicalMesh,
            Issues: finalIssues.AsReadOnly()
        );
    }

    private static byte GetDefaultFormatSizeBytes(uint format) => format switch
    {
        1 => 4,  // Float1
        2 => 8,  // Float2
        3 => 12, // Float3
        4 => 16, // Float4
        5 => 4,  // Byte4
        6 => 4,  // UByte4
        7 => 4,  // Short2
        8 => 8,  // Short4
        _ => 4
    };

    public Ts3GeomImportResult Import(Stream stream, string? nameHint = null)
    {
        if (stream == null || !stream.CanRead)
        {
            var issues = new[] { new ConversionIssue("MESHG000", "Stream is null or unreadable.", ConversionIssueSeverity.Error) };
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues);
        }

        long startPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var result = Import(memoryStream.ToArray(), nameHint);

            if (!result.IsSuccess && stream.CanSeek)
            {
                stream.Position = startPosition;
            }

            return result;
        }
        catch (Exception ex)
        {
            if (stream.CanSeek) stream.Position = startPosition;
            var issues = new[] { new ConversionIssue("MESHG000", $"Error importing GEOM stream: {ex.Message}", ConversionIssueSeverity.Error) };
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues);
        }
    }

    public async Task<Ts3GeomImportResult> ImportAsync(Stream stream, string? nameHint = null, CancellationToken cancellationToken = default)
    {
        if (stream == null || !stream.CanRead)
        {
            var issues = new[] { new ConversionIssue("MESHG000", "Stream is null or unreadable.", ConversionIssueSeverity.Error) };
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues);
        }

        long startPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            var result = Import(memoryStream.ToArray(), nameHint);

            if (!result.IsSuccess && stream.CanSeek)
            {
                stream.Position = startPosition;
            }

            return result;
        }
        catch (Exception ex)
        {
            if (stream.CanSeek) stream.Position = startPosition;
            var issues = new[] { new ConversionIssue("MESHG000", $"Error importing GEOM stream: {ex.Message}", ConversionIssueSeverity.Error) };
            return new Ts3GeomImportResult(IsSuccess: false, Mesh: null, Issues: issues);
        }
    }
}
