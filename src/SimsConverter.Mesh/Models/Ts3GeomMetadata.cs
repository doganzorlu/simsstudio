namespace SimsConverter.Mesh.Models;

public record Ts3GeomMetadata(
    string ContainerFormat,
    uint RcolVersion,
    uint GeomVersion,
    uint VertexCount,
    uint FaceCount,
    uint IndexCount,
    uint VertexStrideBytes,
    uint BoneCount,
    uint ExternalTgiCount,
    uint InternalTgiCount,
    ulong ShaderId,
    long TotalSizeBytes,
    int GeomChunkOffset,
    uint GeomChunkSize
);
