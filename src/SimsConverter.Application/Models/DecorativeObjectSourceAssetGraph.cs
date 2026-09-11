using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectSourceAssetGraph(
    string SourcePackagePath,
    int TotalResourceCount,
    IReadOnlyList<DecorativeObjectSourceMeshAsset> MeshAssets,
    IReadOnlyList<DecorativeObjectSourceTextureAsset> TextureAssets,
    IReadOnlyList<DecorativeObjectSourceResourceLink> ResourceLinks,
    IReadOnlyList<PackageResourceRow> OtherResources,
    bool HasImportableMesh,
    bool HasTextureCandidates,
    bool IsSourceGraphReady,
    IReadOnlyList<ConversionIssue> Issues,
    Ts3ObjectModelDecompositionResult? ObjectModelDecomposition = null
);
