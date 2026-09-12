using System.Collections.Generic;

namespace SimsConverter.Application.Models;

public enum PackageItemCategory
{
    DecorativeObject,
    CasPart,
    MixedCompound,
    Unknown
}

public record PackageItemSummary(
    string ItemId,
    string ItemTitle,
    PackageItemCategory Category,
    uint PrimaryTypeId,
    int MeshCount,
    int TextureCount
);

public record PackageItemClassificationResult(
    string PackagePath,
    PackageItemCategory MainCategory,
    int TotalItemCount,
    IReadOnlyList<PackageItemSummary> Items
);
