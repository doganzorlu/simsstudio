using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record MeshResourceClassification(
    PackageResourceId ResourceId,
    MeshClassificationKind Classification,
    MeshRoleKind RoleKind,
    string FormatName,
    GameVersion DetectedGameVersion,
    IReadOnlyList<ConversionIssue> Issues
);
