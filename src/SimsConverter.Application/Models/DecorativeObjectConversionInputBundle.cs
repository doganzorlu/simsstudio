using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectConversionInputBundle(
    string SourcePackagePath,
    string TargetOutputPath,
    GameVersion TargetGameVersion,
    IReadOnlyList<DecorativeObjectMeshInputBundle> MeshBundles,
    IReadOnlyList<DecorativeObjectSourceTextureAsset> TextureAssets,
    Ts3ObjectModelDecompositionResult? ObjectModelDecomposition,
    IReadOnlyList<PackageResourceRow> RigResources,
    IReadOnlyList<PackageResourceRow> RsltResources,
    IReadOnlyList<DecorativeObjectSourceResourceLink> ResourceLinks,
    bool IsBundleValid,
    IReadOnlyList<ConversionIssue> Issues,
    IReadOnlyList<PackageResourceRow>? OtherResources = null,
    ObjectCatalogMetadata? CatalogMetadata = null
);
