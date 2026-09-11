using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Contracts;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Application.Services;

public class DecorativeObjectConversionInputBundleBuilder : IDecorativeObjectConversionInputBundleBuilder
{
    private const uint RigTypeId = 0x8EAF13DEu;
    private const uint RsltTypeId = 0xD3044521u;

    private readonly IPackageResourcePayloadReader _payloadReader;
    private readonly ITs3GeomCanonicalMeshImporter _ts3GeomImporter;
    private readonly ITs4GeomCanonicalMeshImporter _ts4GeomImporter;
    private readonly ITs3MlodGeometryDecoder _ts3MlodDecoder;
    private readonly ICanonicalMeshValidator _meshValidator;

    public DecorativeObjectConversionInputBundleBuilder(
        IPackageResourcePayloadReader payloadReader,
        ITs3GeomCanonicalMeshImporter ts3GeomImporter,
        ICanonicalMeshValidator meshValidator,
        ITs4GeomCanonicalMeshImporter? ts4GeomImporter = null,
        ITs3MlodGeometryDecoder? ts3MlodDecoder = null)
    {
        _payloadReader = payloadReader ?? throw new ArgumentNullException(nameof(payloadReader));
        _ts3GeomImporter = ts3GeomImporter ?? throw new ArgumentNullException(nameof(ts3GeomImporter));
        _meshValidator = meshValidator ?? throw new ArgumentNullException(nameof(meshValidator));
        _ts4GeomImporter = ts4GeomImporter ?? new Mesh.Services.Ts4GeomCanonicalMeshImporter(new Mesh.Services.Ts4GeomMetadataReader(), _meshValidator);
        _ts3MlodDecoder = ts3MlodDecoder ?? new Mesh.Services.Ts3MlodGeometryDecoder(_meshValidator);
    }

    public async Task<DecorativeObjectConversionInputBundle> BuildBundleAsync(
        DecorativeObjectSourceAssetGraph sourceGraph,
        string targetOutputPath,
        GameVersion targetGameVersion = GameVersion.Sims4,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => BuildBundle(sourceGraph, targetOutputPath, targetGameVersion), cancellationToken).ConfigureAwait(false);
    }

    public DecorativeObjectConversionInputBundle BuildBundle(
        DecorativeObjectSourceAssetGraph sourceGraph,
        string targetOutputPath,
        GameVersion targetGameVersion = GameVersion.Sims4)
    {
        var issues = new List<ConversionIssue>();

        if (sourceGraph == null)
        {
            issues.Add(new ConversionIssue("CONVB000", "Source asset graph is null.", ConversionIssueSeverity.Error));
            return CreateInvalidBundle(string.Empty, targetOutputPath, targetGameVersion, issues);
        }

        if (sourceGraph.Issues != null && sourceGraph.Issues.Count > 0)
        {
            issues.AddRange(sourceGraph.Issues);
        }

        var sourcePackagePath = sourceGraph.SourcePackagePath ?? string.Empty;

        bool hasObjectModel = sourceGraph.ObjectModelDecomposition != null || (sourceGraph.OtherResources != null && sourceGraph.OtherResources.Any(r => r.TypeId == 0x01661233 || r.TypeId == 0x01D10F34));

        if ((!sourceGraph.IsSourceGraphReady && !hasObjectModel) || sourceGraph.MeshAssets == null || (sourceGraph.MeshAssets.Count == 0 && !hasObjectModel))
        {
            issues.Add(new ConversionIssue("CONVB000", "Source asset graph is not ready or contains no verified mesh assets or object model resources.", ConversionIssueSeverity.Error));
            return CreateInvalidBundle(sourcePackagePath, targetOutputPath, targetGameVersion, issues, sourceGraph);
        }

        var meshBundles = new List<DecorativeObjectMeshInputBundle>();
        bool isBundleValid = true;

        foreach (var meshAsset in sourceGraph.MeshAssets)
        {
            if (meshAsset.Entry == null)
            {
                isBundleValid = false;
                issues.Add(new ConversionIssue(
                    "CONVB005",
                    $"Mesh asset {meshAsset.FormattedKey} is missing required package entry metadata.",
                    ConversionIssueSeverity.Error
                ));
                continue;
            }

            var entry = meshAsset.Entry;

            var payloadResult = _payloadReader.ReadPayload(sourcePackagePath, entry);

            if (!payloadResult.IsSuccess || payloadResult.Payload == null)
            {
                isBundleValid = false;
                issues.Add(new ConversionIssue(
                    "CONVB001",
                    $"Canonical mesh import or validation failed for mesh asset {meshAsset.FormattedKey}: payload extraction failed.",
                    ConversionIssueSeverity.Error
                ));
                continue;
            }

            SimsConverter.Domain.Models.CanonicalMesh? importedMesh = null;
            bool importSuccess = false;
            IReadOnlyList<ConversionIssue>? importIssues = null;

            if (meshAsset.DetectedGameVersion == GameVersion.Sims4)
            {
                var importResult = _ts4GeomImporter.Import(payloadResult.Payload, meshAsset.FormattedKey);
                importSuccess = importResult.IsSuccess;
                importedMesh = importResult.Mesh;
                importIssues = importResult.Issues;
            }
            else
            {
                var importResult = _ts3GeomImporter.Import(payloadResult.Payload, meshAsset.FormattedKey);
                importSuccess = importResult.IsSuccess;
                importedMesh = importResult.Mesh;
                importIssues = importResult.Issues;
            }

            if (!importSuccess || importedMesh == null)
            {
                isBundleValid = false;
                issues.Add(new ConversionIssue(
                    "CONVB001",
                    $"Canonical mesh import or validation failed for mesh asset {meshAsset.FormattedKey}: canonical importer failed.",
                    ConversionIssueSeverity.Error
                ));
                if (importIssues != null)
                {
                    issues.AddRange(importIssues);
                }
                continue;
            }

            var validationResult = _meshValidator.Validate(importedMesh);
            if (!validationResult.IsSuccess)
            {
                isBundleValid = false;
                issues.Add(new ConversionIssue(
                    "CONVB001",
                    $"Canonical mesh import or validation failed for mesh asset {meshAsset.FormattedKey}: domain validator failed.",
                    ConversionIssueSeverity.Error
                ));
                if (validationResult.Issues != null)
                {
                    issues.AddRange(validationResult.Issues);
                }
                continue;
            }

            uint? lodIndex = null;
            uint? groupIndex = null;
            string? materialRefKey = null;

            if (sourceGraph.ObjectModelDecomposition?.ModelMetadataResults != null)
            {
                foreach (var meta in sourceGraph.ObjectModelDecomposition.ModelMetadataResults)
                {
                    if (meta.GeometryReferences != null)
                    {
                        var geomRef = meta.GeometryReferences.FirstOrDefault(g => g.TargetResourceId != null && g.TargetResourceId.Equals(meshAsset.ResourceId));
                        if (geomRef != null)
                        {
                            groupIndex = geomRef.GroupIndex;
                            materialRefKey = geomRef.ReferenceType;
                            break;
                        }
                    }
                }
            }

            if (!lodIndex.HasValue)
            {
                lodIndex = entry.Id.GroupId;
            }

            meshBundles.Add(new DecorativeObjectMeshInputBundle(
                ResourceId: meshAsset.ResourceId,
                FormattedKey: meshAsset.FormattedKey,
                CanonicalMesh: importedMesh,
                RawPayload: Array.AsReadOnly(payloadResult.Payload.ToArray()),
                AssociatedLodIndex: lodIndex,
                AssociatedGroupIndex: groupIndex,
                MaterialReferenceKey: materialRefKey,
                Issues: meshAsset.Issues
            ));
        }

        if (meshBundles.Count == 0 && hasObjectModel)
        {
            var modelRows = new List<PackageResourceRow>();

            if (sourceGraph.ObjectModelDecomposition != null)
            {
                if (sourceGraph.ObjectModelDecomposition.MlodResources != null)
                    modelRows.AddRange(sourceGraph.ObjectModelDecomposition.MlodResources);
                if (sourceGraph.ObjectModelDecomposition.ModlResources != null)
                    modelRows.AddRange(sourceGraph.ObjectModelDecomposition.ModlResources);
            }

            if (sourceGraph.OtherResources != null)
            {
                foreach (var r in sourceGraph.OtherResources)
                {
                    if ((r.TypeId == 0x01661233 || r.TypeId == 0x01D10F34) &&
                        !modelRows.Any(e => e.FormattedKey.Equals(r.FormattedKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        modelRows.Add(r);
                    }
                }
            }

            uint lodIdxCounter = 0;
            foreach (var modelRow in modelRows)
            {
                var entry = modelRow.ToEntry();
                var payloadResult = _payloadReader.ReadPayload(sourcePackagePath, entry);
                if (!payloadResult.IsSuccess || payloadResult.Payload == null) continue;

                var decodeResult = _ts3MlodDecoder.Decode(payloadResult.Payload, modelRow.FormattedKey);
                if (decodeResult.IsSuccess && decodeResult.Mesh != null)
                {
                    var geomResourceId = new PackageResourceId(
                        SimsConverter.Domain.Constants.Ts4ResourceTypeIds.Geom,
                        modelRow.GroupId,
                        modelRow.InstanceId != 0 ? modelRow.InstanceId : (ulong)lodIdxCounter + 1
                    );

                    meshBundles.Add(new DecorativeObjectMeshInputBundle(
                        ResourceId: geomResourceId,
                        FormattedKey: geomResourceId.FormattedKey,
                        CanonicalMesh: decodeResult.Mesh,
                        RawPayload: Array.AsReadOnly(payloadResult.Payload.ToArray()),
                        AssociatedLodIndex: lodIdxCounter++,
                        AssociatedGroupIndex: 0,
                        MaterialReferenceKey: "MLOD_Extracted",
                        Issues: decodeResult.Issues
                    ));
                }
            }
        }

        if (meshBundles.Count == 0)
        {
            isBundleValid = false;
            issues.Add(new ConversionIssue(
                "CONVG007",
                "No valid geometry data could be extracted from source package (neither TS3 GEOM resources nor embedded MODL/MLOD mesh streams were found).",
                ConversionIssueSeverity.Error
            ));
        }

        // CONVB002: Missing Texture Candidates Diagnostic
        if (sourceGraph.TextureAssets == null || sourceGraph.TextureAssets.Count == 0)
        {
            issues.Add(new ConversionIssue(
                "CONVB002",
                "No valid texture candidates found in source package for conversion bundle.",
                ConversionIssueSeverity.Warning
            ));
        }

        var rigResources = new List<PackageResourceRow>();
        var rsltResources = new List<PackageResourceRow>();

        if (sourceGraph.ObjectModelDecomposition != null)
        {
            if (sourceGraph.ObjectModelDecomposition.RigResources != null)
                rigResources.AddRange(sourceGraph.ObjectModelDecomposition.RigResources);
            if (sourceGraph.ObjectModelDecomposition.RsltResources != null)
                rsltResources.AddRange(sourceGraph.ObjectModelDecomposition.RsltResources);
        }

        if (sourceGraph.OtherResources != null)
        {
            foreach (var r in sourceGraph.OtherResources)
            {
                if (r.TypeId == RigTypeId && !rigResources.Any(existing => existing.FormattedKey.Equals(r.FormattedKey, StringComparison.OrdinalIgnoreCase)))
                {
                    rigResources.Add(r);
                }
                else if (r.TypeId == RsltTypeId && !rsltResources.Any(existing => existing.FormattedKey.Equals(r.FormattedKey, StringComparison.OrdinalIgnoreCase)))
                {
                    rsltResources.Add(r);
                }
            }
        }

        if (sourceGraph.MeshAssets != null)
        {
            foreach (var m in sourceGraph.MeshAssets)
            {
                if (m.Entry != null)
                {
                    if (m.ResourceId.TypeId == RigTypeId && !rigResources.Any(existing => existing.FormattedKey.Equals(m.FormattedKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        rigResources.Add(PackageResourceRow.FromEntry(m.Entry));
                    }
                    else if (m.ResourceId.TypeId == RsltTypeId && !rsltResources.Any(existing => existing.FormattedKey.Equals(m.FormattedKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        rsltResources.Add(PackageResourceRow.FromEntry(m.Entry));
                    }
                }
            }
        }

        // CONVB003: Missing Skeleton (RIG) Diagnostic when bone weights are present
        bool requiresSkeleton = meshBundles.Any(b => b.CanonicalMesh.Vertices.Any(v => v != null && v.BoneWeights != null && v.BoneWeights.Count > 0));
        if (requiresSkeleton && rigResources.Count == 0)
        {
            issues.Add(new ConversionIssue(
                "CONVB003",
                "Mesh candidate requires skeleton weights but no RIG resource (0x8EAF13DE) was found in source package.",
                ConversionIssueSeverity.Warning
            ));
        }

        // CONVB004: Unverified Material Reference Diagnostic
        bool hasUnverifiedMaterialRef = meshBundles.Any(b => !string.IsNullOrEmpty(b.MaterialReferenceKey) && b.MaterialReferenceKey.Contains("Heuristic", StringComparison.OrdinalIgnoreCase));
        if (hasUnverifiedMaterialRef)
        {
            issues.Add(new ConversionIssue(
                "CONVB004",
                "Unverified material reference key detected in conversion bundle.",
                ConversionIssueSeverity.Warning
            ));
        }

        meshBundles.Sort((a, b) => string.Compare(a.FormattedKey, b.FormattedKey, StringComparison.Ordinal));

        var processedTextures = new List<DecorativeObjectSourceTextureAsset>();
        if (sourceGraph.TextureAssets != null)
        {
            foreach (var texAsset in sourceGraph.TextureAssets)
            {
                IReadOnlyList<byte>? rawPayload = texAsset.RawPayload;
                if (rawPayload == null && texAsset.Entry != null)
                {
                    var payloadResult = _payloadReader.ReadPayload(sourcePackagePath, texAsset.Entry);
                    if (payloadResult.IsSuccess && payloadResult.Payload != null)
                    {
                        rawPayload = Array.AsReadOnly(payloadResult.Payload.ToArray());
                    }
                }

                processedTextures.Add(texAsset with { RawPayload = rawPayload });
            }
        }

        processedTextures.Sort((a, b) => string.Compare(a.FormattedKey, b.FormattedKey, StringComparison.Ordinal));

        rigResources.Sort((a, b) => string.Compare(a.FormattedKey, b.FormattedKey, StringComparison.Ordinal));
        rsltResources.Sort((a, b) => string.Compare(a.FormattedKey, b.FormattedKey, StringComparison.Ordinal));

        var resourceLinks = sourceGraph.ResourceLinks ?? Array.Empty<DecorativeObjectSourceResourceLink>();

        return new DecorativeObjectConversionInputBundle(
            SourcePackagePath: sourcePackagePath,
            TargetOutputPath: targetOutputPath ?? string.Empty,
            TargetGameVersion: targetGameVersion,
            MeshBundles: meshBundles.AsReadOnly(),
            TextureAssets: processedTextures.AsReadOnly(),
            ObjectModelDecomposition: sourceGraph.ObjectModelDecomposition,
            RigResources: rigResources.AsReadOnly(),
            RsltResources: rsltResources.AsReadOnly(),
            ResourceLinks: resourceLinks,
            IsBundleValid: isBundleValid && (meshBundles.Count > 0 || hasObjectModel),
            Issues: issues.AsReadOnly(),
            OtherResources: sourceGraph.OtherResources,
            CatalogMetadata: sourceGraph.CatalogMetadata
        );
    }

    private static DecorativeObjectConversionInputBundle CreateInvalidBundle(
        string sourcePackagePath,
        string targetOutputPath,
        GameVersion targetGameVersion,
        List<ConversionIssue> issues,
        DecorativeObjectSourceAssetGraph? sourceGraph = null)
    {
        return new DecorativeObjectConversionInputBundle(
            SourcePackagePath: sourcePackagePath,
            TargetOutputPath: targetOutputPath ?? string.Empty,
            TargetGameVersion: targetGameVersion,
            MeshBundles: Array.Empty<DecorativeObjectMeshInputBundle>(),
            TextureAssets: sourceGraph?.TextureAssets ?? Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ObjectModelDecomposition: sourceGraph?.ObjectModelDecomposition,
            RigResources: Array.Empty<PackageResourceRow>(),
            RsltResources: Array.Empty<PackageResourceRow>(),
            ResourceLinks: sourceGraph?.ResourceLinks ?? Array.Empty<DecorativeObjectSourceResourceLink>(),
            IsBundleValid: false,
            Issues: issues.AsReadOnly(),
            OtherResources: sourceGraph?.OtherResources,
            CatalogMetadata: sourceGraph?.CatalogMetadata
        );
    }
}
