using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record MeshResourceRow(
    string FormattedKey,
    string TypeHex,
    string GroupHex,
    string InstanceHex,
    uint DataOffset,
    uint CompressedSize,
    uint DecompressedSize,
    string CompressionName,
    MeshClassificationKind ClassificationKind,
    MeshRoleKind RoleKind,
    string FormatName,
    GameVersion DetectedGameVersion,
    bool CanExtractRawPayload,
    bool CanInspectCanonicalMesh,
    uint? VertexCount,
    uint? FaceCount,
    uint? BoneCount,
    bool HasNormals,
    bool HasUv0,
    bool HasBoneWeights,
    int ValidationIssueCount,
    IReadOnlyList<ConversionIssue> Issues,
    PackageResourceEntry Entry,
    MeshResourceClassification Classification
);
