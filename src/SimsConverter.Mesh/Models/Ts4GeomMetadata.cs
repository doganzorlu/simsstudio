namespace SimsConverter.Mesh.Models;

public record Ts4GeomMetadata(
    string ContainerFormat,
    uint GeomVersion,
    uint ShaderHash,
    uint MtnfSizeBytes,
    int MergeGroup,
    int SortOrder,
    uint VertexCount,
    uint FaceCount,
    uint FacePointCount,
    uint VertexStrideBytes,
    uint VertexElementCount,
    uint NumSubMeshes,
    int UvStitchCount,
    int SeamStitchCount,
    int SlotrayCount,
    uint BoneCount,
    uint EmbeddedTgiCount,
    long TotalSizeBytes,
    int GeomChunkOffset,
    uint GeomChunkSize,
    uint TgiOffset,
    uint TgiSize
);
