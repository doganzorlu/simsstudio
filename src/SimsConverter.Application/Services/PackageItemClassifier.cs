using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Constants;
using SimsConverter.Package.Models;

namespace SimsConverter.Application.Services;

public class PackageItemClassifier : IPackageItemClassifier
{
    public PackageItemClassificationResult ClassifyPackage(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources)
    {
        if (string.IsNullOrEmpty(packagePath) || packageResources == null || packageResources.Count == 0)
        {
            return new PackageItemClassificationResult(packagePath ?? string.Empty, PackageItemCategory.Unknown, 0, Array.Empty<PackageItemSummary>());
        }

        var items = new List<PackageItemSummary>();

        // 1. Discover Object Catalog Items
        var objectRows = packageResources.Where(r => r.TypeId == Ts4ResourceTypeIds.ObjectDefinition || r.TypeId == Ts4ResourceTypeIds.CatalogObject).ToList();
        foreach (var r in objectRows)
        {
            int meshCount = packageResources.Count(res => res.TypeId == Ts4ResourceTypeIds.Geom || res.TypeId == Ts4ResourceTypeIds.ModelLod);
            int texCount = packageResources.Count(res => res.TypeId == Ts4ResourceTypeIds.Rle2Texture || res.TypeId == 0x00B2D882u);

            items.Add(new PackageItemSummary(
                ItemId: r.FormattedKey,
                ItemTitle: $"Object_{r.InstanceHex}",
                Category: PackageItemCategory.DecorativeObject,
                PrimaryTypeId: r.TypeId,
                MeshCount: meshCount,
                TextureCount: texCount
            ));
        }

        // 2. Discover CAS Part Items
        var caspRows = packageResources.Where(r => r.TypeId == Ts4ResourceTypeIds.CasPartTS3 || r.TypeId == Ts4ResourceTypeIds.CasPartTS4).ToList();
        foreach (var r in caspRows)
        {
            int meshCount = packageResources.Count(res => res.TypeId == Ts4ResourceTypeIds.Geom);
            int texCount = packageResources.Count(res => res.TypeId == Ts4ResourceTypeIds.Rle2Texture || res.TypeId == 0x00B2D882u);

            items.Add(new PackageItemSummary(
                ItemId: r.FormattedKey,
                ItemTitle: $"CASP_{r.InstanceHex}",
                Category: PackageItemCategory.CasPart,
                PrimaryTypeId: r.TypeId,
                MeshCount: meshCount,
                TextureCount: texCount
            ));
        }

        // 3. Handle Fallback for Objects without explicit OBJD row (e.g. MODL/MLOD embedded models)
        if (items.Count == 0)
        {
            bool hasModl = packageResources.Any(r => r.TypeId == Ts4ResourceTypeIds.Model || r.TypeId == Ts4ResourceTypeIds.ModelLod);
            if (hasModl)
            {
                int meshCount = packageResources.Count(res => res.TypeId == Ts4ResourceTypeIds.Geom || res.TypeId == Ts4ResourceTypeIds.ModelLod);
                int texCount = packageResources.Count(res => res.TypeId == Ts4ResourceTypeIds.Rle2Texture || res.TypeId == 0x00B2D882u);

                items.Add(new PackageItemSummary(
                    ItemId: "EmbeddedObject",
                    ItemTitle: "Embedded Object Model",
                    Category: PackageItemCategory.DecorativeObject,
                    PrimaryTypeId: Ts4ResourceTypeIds.Model,
                    MeshCount: meshCount,
                    TextureCount: texCount
                ));
            }
        }

        PackageItemCategory mainCat;
        if (items.Count == 0)
        {
            mainCat = PackageItemCategory.Unknown;
        }
        else if (items.All(i => i.Category == PackageItemCategory.DecorativeObject))
        {
            mainCat = PackageItemCategory.DecorativeObject;
        }
        else if (items.All(i => i.Category == PackageItemCategory.CasPart))
        {
            mainCat = PackageItemCategory.CasPart;
        }
        else
        {
            mainCat = PackageItemCategory.MixedCompound;
        }

        return new PackageItemClassificationResult(packagePath, mainCat, items.Count, items.AsReadOnly());
    }

    public async Task<PackageItemClassificationResult> ClassifyPackageAsync(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ClassifyPackage(packagePath, packageResources), cancellationToken).ConfigureAwait(false);
    }
}
