using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectSourceMeshAsset(
    string FormattedKey,
    PackageResourceId ResourceId,
    MeshClassificationKind ClassificationKind,
    MeshRoleKind RoleKind,
    string FormatName,
    GameVersion DetectedGameVersion,
    bool CanInspectCanonicalMesh,
    uint? VertexCount,
    uint? FaceCount,
    uint? BoneCount,
    IReadOnlyList<ConversionIssue> Issues,
    PackageResourceEntry? Entry = null
);
