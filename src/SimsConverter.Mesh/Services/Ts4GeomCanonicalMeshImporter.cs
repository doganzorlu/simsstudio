using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Contracts;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Services;

public class Ts4GeomCanonicalMeshImporter : ITs4GeomCanonicalMeshImporter
{
    private readonly ITs4GeomMetadataReader _metadataReader;
    private readonly ICanonicalMeshValidator _validator;

    public Ts4GeomCanonicalMeshImporter(ITs4GeomMetadataReader metadataReader, ICanonicalMeshValidator validator)
    {
        _metadataReader = metadataReader ?? throw new ArgumentNullException(nameof(metadataReader));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public Task<Ts4GeomImportResult> ImportAsync(byte[] buffer, string? meshName = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Import(buffer, meshName), cancellationToken);
    }

    public Ts4GeomImportResult Import(byte[] buffer, string? meshName = null)
    {
        if (buffer == null)
        {
            return Failure(new ConversionIssue("MESHG000", "Buffer is null.", ConversionIssueSeverity.Error));
        }

        var parseResult = _metadataReader.ReadMetadata(buffer, meshName);
        if (!parseResult.IsSuccess || parseResult.Metadata == null)
        {
            return Failure(parseResult.Issues);
        }

        var meta = parseResult.Metadata;
        var issues = new List<ConversionIssue>(parseResult.Issues);

        int geomOffset = meta.GeomChunkOffset;
        int geomSize = (int)meta.GeomChunkSize;

        if (geomOffset < 0 || geomOffset + geomSize > buffer.Length)
        {
            return Failure(new ConversionIssue("MESHG001", "GeomChunkOffset/Size specified in metadata exceeds buffer boundaries.", ConversionIssueSeverity.Error));
        }

        ReadOnlySpan<byte> geomSpan = buffer.AsSpan(geomOffset, geomSize);

        try
        {
            checked
            {
                uint shaderHash = meta.ShaderHash;
                int curr = 20;

                if (shaderHash != 0)
                {
                    if (curr + 4 > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("MESHG002", "GEOM payload truncated reading MTNF size.", ConversionIssueSeverity.Error));
                    }
                    uint mtnfSizeBytes = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr, 4));
                    curr += 4 + (int)mtnfSizeBytes;
                }

                // Skip mergeGroup (4B), sortOrder (4B), vertexCount (4B), elementCount (4B)
                curr += 16;

                int positionOffset = -1;
                int normalOffset = -1;
                int tangentOffset = -1;
                int uv0Offset = -1;
                int boneIndicesOffset = -1;
                int boneWeightsOffset = -1;

                int boneIndicesFormat = -1;
                int boneWeightsFormat = -1;
                byte boneWeightsSizeBytes = 0;

                int elementOffset = 0;
                for (int i = 0; i < (int)meta.VertexElementCount; i++)
                {
                    if (curr + 9 > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("MESHG002", $"GEOM payload truncated reading descriptor {i}.", ConversionIssueSeverity.Error));
                    }

                    uint datatype = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr, 4));
                    uint format = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr + 4, 4));
                    byte sizeBytes = geomSpan[curr + 8];

                    switch (datatype)
                    {
                        case 1: // Position
                            positionOffset = elementOffset;
                            break;
                        case 2: // Normal
                            normalOffset = elementOffset;
                            break;
                        case 3: // UV0
                            if (uv0Offset < 0) uv0Offset = elementOffset;
                            break;
                        case 4: // Bone Indices
                            boneIndicesOffset = elementOffset;
                            boneIndicesFormat = (int)format;
                            break;
                        case 5: // Bone Weights
                            boneWeightsOffset = elementOffset;
                            boneWeightsFormat = (int)format;
                            boneWeightsSizeBytes = sizeBytes;

                            if (!((format == 1 && sizeBytes >= 16) || (format == 2 && sizeBytes >= 4)))
                            {
                                return Failure(new ConversionIssue(
                                    "MESHG008",
                                    $"Unsupported or invalid bone weight format (Format={format}, Size={sizeBytes}B). Supported: Format 1 (Float4 >= 16B) or Format 2 (Byte4 >= 4B).",
                                    ConversionIssueSeverity.Error
                                ));
                            }
                            break;
                        case 6: // Tangent
                            tangentOffset = elementOffset;
                            break;
                        case 10: // Vertex ID
                            break;
                        default:
                            issues.Add(new ConversionIssue(
                                "MESHG003",
                                $"Unrecognized vertex element datatype {datatype} (Format={format}, Size={sizeBytes}B). Cursor advanced by {sizeBytes} bytes.",
                                ConversionIssueSeverity.Warning
                            ));
                            break;
                    }

                    elementOffset += sizeBytes;
                    curr += 9;
                }

                if (positionOffset < 0)
                {
                    return Failure(new ConversionIssue("MESHG004", "Missing Position vertex element descriptor in GEOM header.", ConversionIssueSeverity.Error));
                }

                int stride = (int)meta.VertexStrideBytes;
                int totalVertexBufferBytes = (int)(meta.VertexCount * stride);

                if (curr + totalVertexBufferBytes > geomSpan.Length)
                {
                    return Failure(new ConversionIssue("MESHG002", "GEOM vertex buffer payload is truncated.", ConversionIssueSeverity.Error));
                }

                var vertices = new List<CanonicalVertex>((int)meta.VertexCount);

                for (int v = 0; v < (int)meta.VertexCount; v++)
                {
                    int vStart = curr + (v * stride);

                    // Position (float3)
                    float x = BitConverter.ToSingle(geomSpan.Slice(vStart + positionOffset, 4));
                    float y = BitConverter.ToSingle(geomSpan.Slice(vStart + positionOffset + 4, 4));
                    float z = BitConverter.ToSingle(geomSpan.Slice(vStart + positionOffset + 8, 4));
                    var pos = new MeshVector3(x, y, z);

                    // Normal (float3)
                    MeshVector3? norm = null;
                    if (normalOffset >= 0)
                    {
                        float nx = BitConverter.ToSingle(geomSpan.Slice(vStart + normalOffset, 4));
                        float ny = BitConverter.ToSingle(geomSpan.Slice(vStart + normalOffset + 4, 4));
                        float nz = BitConverter.ToSingle(geomSpan.Slice(vStart + normalOffset + 8, 4));
                        norm = new MeshVector3(nx, ny, nz);
                    }

                    // Tangent (float4)
                    MeshVector4? tang = null;
                    if (tangentOffset >= 0)
                    {
                        float tx = BitConverter.ToSingle(geomSpan.Slice(vStart + tangentOffset, 4));
                        float ty = BitConverter.ToSingle(geomSpan.Slice(vStart + tangentOffset + 4, 4));
                        float tz = BitConverter.ToSingle(geomSpan.Slice(vStart + tangentOffset + 8, 4));
                        float tw = BitConverter.ToSingle(geomSpan.Slice(vStart + tangentOffset + 12, 4));
                        tang = new MeshVector4(tx, ty, tz, tw);
                    }

                    // UV0 (float2)
                    MeshVector2? uv0 = null;
                    if (uv0Offset >= 0)
                    {
                        float u = BitConverter.ToSingle(geomSpan.Slice(vStart + uv0Offset, 4));
                        float vCoord = BitConverter.ToSingle(geomSpan.Slice(vStart + uv0Offset + 4, 4));
                        uv0 = new MeshVector2(u, vCoord);
                    }

                    // Bone Weights (format == 1 -> Float4, format == 2 -> Byte4 normalized)
                    var boneWeights = new List<CanonicalBoneWeight>();
                    if (boneIndicesOffset >= 0 && boneWeightsOffset >= 0)
                    {
                        for (int b = 0; b < 4; b++)
                        {
                            byte boneIdx = geomSpan[vStart + boneIndicesOffset + b];
                            float weight = 0.0f;

                            if (boneWeightsFormat == 1) // Float4
                            {
                                weight = BitConverter.ToSingle(geomSpan.Slice(vStart + boneWeightsOffset + (b * 4), 4));
                            }
                            else if (boneWeightsFormat == 2) // Byte4 normalized
                            {
                                weight = geomSpan[vStart + boneWeightsOffset + b] / 255.0f;
                            }

                            if (weight > 0.0001f)
                            {
                                boneWeights.Add(new CanonicalBoneWeight(boneIdx, weight));
                            }
                        }
                    }

                    vertices.Add(new CanonicalVertex(
                        position: pos,
                        normal: norm,
                        tangent: tang,
                        uv0: uv0,
                        uv1: null,
                        boneWeights: boneWeights
                    ));
                }

                curr += totalVertexBufferBytes;

                // Submesh Face Section
                if (curr + 4 > geomSpan.Length)
                {
                    return Failure(new ConversionIssue("MESHG002", "GEOM payload truncated reading numSubMeshes.", ConversionIssueSeverity.Error));
                }

                uint numSubMeshes = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr, 4));
                curr += 4;

                var faces = new List<CanonicalFace>();

                for (int s = 0; s < numSubMeshes; s++)
                {
                    if (curr + 5 > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("MESHG002", $"GEOM payload truncated reading submesh {s} header.", ConversionIssueSeverity.Error));
                    }

                    byte bytesPerFacePoint = geomSpan[curr];
                    curr += 1;

                    uint numFacePoints = BinaryPrimitives.ReadUInt32LittleEndian(geomSpan.Slice(curr, 4));
                    curr += 4;

                    int facePointSize = bytesPerFacePoint == 0 ? 2 : bytesPerFacePoint;

                    if (facePointSize != 1 && facePointSize != 2 && facePointSize != 4)
                    {
                        return Failure(new ConversionIssue("MESHG007", $"Unsupported bytesPerFacePoint value: {bytesPerFacePoint}.", ConversionIssueSeverity.Error));
                    }

                    int submeshFaceBufferBytes = (int)(numFacePoints * facePointSize);
                    if (curr + submeshFaceBufferBytes > geomSpan.Length)
                    {
                        return Failure(new ConversionIssue("MESHG002", $"GEOM index buffer payload is truncated in submesh {s}.", ConversionIssueSeverity.Error));
                    }

                    int triangleCount = (int)(numFacePoints / 3);
                    for (int t = 0; t < triangleCount; t++)
                    {
                        int fStart = curr + (t * 3 * facePointSize);
                        int i0 = ReadIndex(geomSpan, fStart, facePointSize);
                        int i1 = ReadIndex(geomSpan, fStart + facePointSize, facePointSize);
                        int i2 = ReadIndex(geomSpan, fStart + (2 * facePointSize), facePointSize);

                        faces.Add(new CanonicalFace(i0, i1, i2));
                    }

                    curr += submeshFaceBufferBytes;
                }

                string name = !string.IsNullOrWhiteSpace(meshName) ? meshName : "TS4_GEOM_Mesh";
                var materials = new[] { new CanonicalMaterialSlot(0, "DefaultMaterial") };

                var canonicalMesh = new CanonicalMesh(
                    name: name,
                    vertices: vertices,
                    faces: faces,
                    materials: materials,
                    coordinateSystem: CanonicalCoordinateSystem.RightHandedYUp,
                    sourceGameVersion: GameVersion.Sims4,
                    issues: issues
                );

                var validationResult = _validator.Validate(canonicalMesh);
                var finalIssues = issues.Concat(validationResult.Issues).ToList();

                bool overallSuccess = validationResult.IsSuccess && !finalIssues.Any(i => i.Severity == ConversionIssueSeverity.Error || i.Severity == ConversionIssueSeverity.Fatal);

                return new Ts4GeomImportResult(overallSuccess, canonicalMesh, finalIssues.AsReadOnly());
            }
        }
        catch (OverflowException)
        {
            return Failure(new ConversionIssue("MESHG002", "Arithmetic overflow detected during binary vertex/index decoding.", ConversionIssueSeverity.Error));
        }
        catch (Exception ex)
        {
            return Failure(new ConversionIssue("MESHG002", $"Failed to decode TS4 GEOM mesh: {ex.Message}", ConversionIssueSeverity.Error));
        }
    }

    private static int ReadIndex(ReadOnlySpan<byte> span, int offset, int bytesPerPoint)
    {
        return bytesPerPoint switch
        {
            1 => span[offset],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2)),
            4 => (int)BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)),
            _ => 0
        };
    }

    private static Ts4GeomImportResult Failure(ConversionIssue issue)
    {
        return new Ts4GeomImportResult(false, null, new[] { issue });
    }

    private static Ts4GeomImportResult Failure(IEnumerable<ConversionIssue> issues)
    {
        return new Ts4GeomImportResult(false, null, new List<ConversionIssue>(issues).AsReadOnly());
    }
}
