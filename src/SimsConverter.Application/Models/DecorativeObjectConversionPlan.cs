using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Application.Models;

public record DecorativeObjectConversionPlan(
    string SourcePackagePath,
    string TargetOutputPath,
    GameVersion TargetGameVersion,
    int TotalMeshCandidateCount,
    int TotalTextureCandidateCount,
    int ConvertableMeshCount,
    int ValidTextureCount,
    IReadOnlyList<MeshResourceRow> MeshCandidates,
    IReadOnlyList<TextureResourceRow> TextureCandidates,
    IReadOnlyList<DecorativeObjectConversionStep> Steps,
    bool IsFeasible,
    DecorativeObjectSourceAssetGraph? SourceGraph = null,
    DecorativeObjectConversionInputBundle? InputBundle = null,
    SimsConverter.Domain.Models.DecorativeObjectConversionCapabilityMatrix? CapabilityMatrix = null
);
