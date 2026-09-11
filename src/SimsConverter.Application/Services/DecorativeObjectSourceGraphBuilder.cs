using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Services;

public class DecorativeObjectSourceGraphBuilder : IDecorativeObjectSourceGraphBuilder
{
    // TS3 GEOM Magic TypeId Constant
    private const uint Ts3GeomTypeId = 0x015A1849u;
    // TS4 Object Model / Model LOD TypeId Constants
    private const uint TsModelTypeId = 0x01661233u;
    private const uint TsModelLodTypeId = 0x01D10F34u;

    private readonly IPackageInspectionService _packageInspectionService;
    private readonly IMeshInspectionService _meshInspectionService;
    private readonly ITextureInspectionService _textureInspectionService;
    private readonly ITs3ObjectModelDecompositionService? _decompositionService;
    private readonly ITs3CatalogMetadataReader _catalogMetadataReader;

    public DecorativeObjectSourceGraphBuilder(
        IPackageInspectionService packageInspectionService,
        IMeshInspectionService meshInspectionService,
        ITextureInspectionService textureInspectionService,
        ITs3ObjectModelDecompositionService? decompositionService = null,
        ITs3CatalogMetadataReader? catalogMetadataReader = null)
    {
        _packageInspectionService = packageInspectionService ?? throw new ArgumentNullException(nameof(packageInspectionService));
        _meshInspectionService = meshInspectionService ?? throw new ArgumentNullException(nameof(meshInspectionService));
        _textureInspectionService = textureInspectionService ?? throw new ArgumentNullException(nameof(textureInspectionService));
        _decompositionService = decompositionService;
        _catalogMetadataReader = catalogMetadataReader ?? new Ts3CatalogMetadataReader();
    }

    public async Task<DecorativeObjectSourceAssetGraph> BuildGraphAsync(
        string packageFilePath,
        GameVersion sourceGameVersionHint = GameVersion.Unknown,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageFilePath))
        {
            return CreateFailureGraph(
                packageFilePath ?? string.Empty,
                "CONVG000",
                "Package file path is null or empty."
            );
        }

        var packageResult = await _packageInspectionService.InspectFileAsync(packageFilePath, cancellationToken).ConfigureAwait(false);
        if (!packageResult.IsSuccess)
        {
            return CreateFailureGraph(
                packageFilePath,
                "CONVG001",
                $"Package inspection failed for file: {packageFilePath}",
                packageResult.Issues
            );
        }

        return BuildGraph(packageResult, sourceGameVersionHint);
    }

    public DecorativeObjectSourceAssetGraph BuildGraph(
        PackageInspectionResult packageInspection,
        GameVersion sourceGameVersionHint = GameVersion.Unknown)
    {
        if (packageInspection == null)
        {
            return CreateFailureGraph(
                string.Empty,
                "CONVG000",
                "Package inspection result is null."
            );
        }

        var sourcePath = packageInspection.FilePath ?? string.Empty;
        var issues = new List<ConversionIssue>();
        if (packageInspection.Issues != null)
        {
            issues.AddRange(packageInspection.Issues);
        }

        var effectiveSourceVersion = sourceGameVersionHint;
        if (effectiveSourceVersion == GameVersion.Unknown)
        {
            // COBJ/MODL/MLOD are shared by TS3 and TS4 packages. Require a TS4-specific
            // marker before selecting the TS4 mesh/importer dispatch.
            bool hasTs4Resources = packageInspection.Resources != null &&
                packageInspection.Resources.Any(r =>
                    r.TypeId == 0xC0DB5AE7u || // TS4 OBJD
                    r.TypeId == 0x2172D019u || // TS4 RMAT
                    r.TypeId == 0x2BC04EDFu || // TS4 LRLE
                    r.TypeId == 0x3453CF95u || // TS4 RLE2
                    r.TypeId == 0x2F7D0004u);  // TS4 PNG image

            effectiveSourceVersion = hasTs4Resources ? GameVersion.Sims4 : GameVersion.Sims3;
        }

        var meshInspection = _meshInspectionService.InspectPackageMeshes(packageInspection, effectiveSourceVersion);
        if (meshInspection.Issues != null)
        {
            issues.AddRange(meshInspection.Issues);
        }

        var textureInspection = _textureInspectionService.InspectPackageTextures(packageInspection, effectiveSourceVersion);
        if (textureInspection.Issues != null)
        {
            issues.AddRange(textureInspection.Issues);
        }

        var meshAssets = new List<DecorativeObjectSourceMeshAsset>();
        var textureAssets = new List<DecorativeObjectSourceTextureAsset>();
        var otherResources = new List<PackageResourceRow>();
        var links = new List<DecorativeObjectSourceResourceLink>();

        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var meshRow in meshInspection.Rows)
        {
            // Direct GEOM mesh asset acceptance criteria (5 mandatory conditions):
            // 1. ClassificationKind == KnownMesh
            // 2. DetectedGameVersion == Sims3 or Sims4
            // 3. RoleKind == Geometry
            // 4. Entry.Id.TypeId == 0x015A1849 (GEOM)
            // 5. CanInspectCanonicalMesh == true
            if (meshRow.ClassificationKind == MeshClassificationKind.KnownMesh &&
                (meshRow.DetectedGameVersion == GameVersion.Sims3 || meshRow.DetectedGameVersion == GameVersion.Sims4) &&
                meshRow.RoleKind == MeshRoleKind.Geometry &&
                meshRow.Entry.Id.TypeId == Ts3GeomTypeId &&
                meshRow.CanInspectCanonicalMesh)
            {
                processedKeys.Add(meshRow.FormattedKey);
                meshAssets.Add(new DecorativeObjectSourceMeshAsset(
                    FormattedKey: meshRow.FormattedKey,
                    ResourceId: meshRow.Entry.Id,
                    ClassificationKind: meshRow.ClassificationKind,
                    RoleKind: meshRow.RoleKind,
                    FormatName: meshRow.FormatName,
                    DetectedGameVersion: meshRow.DetectedGameVersion,
                    CanInspectCanonicalMesh: meshRow.CanInspectCanonicalMesh,
                    VertexCount: meshRow.VertexCount,
                    FaceCount: meshRow.FaceCount,
                    BoneCount: meshRow.BoneCount,
                    Issues: meshRow.Issues ?? Array.Empty<ConversionIssue>(),
                    Entry: meshRow.Entry
                ));
            }
        }

        foreach (var texRow in textureInspection.Rows)
        {
            if (texRow.ClassificationKind == TextureClassificationKind.KnownTexture)
            {
                processedKeys.Add(texRow.FormattedKey);
                textureAssets.Add(new DecorativeObjectSourceTextureAsset(
                    FormattedKey: texRow.FormattedKey,
                    ResourceId: texRow.Entry.Id,
                    ClassificationKind: texRow.ClassificationKind,
                    MapKind: texRow.MapKind,
                    FormatName: texRow.FormatName,
                    CanExtractRawPayload: texRow.CanExtractRawPayload,
                    Issues: texRow.Issues ?? Array.Empty<ConversionIssue>(),
                    Entry: texRow.Entry
                ));
            }
        }

        if (packageInspection.Resources != null)
        {
            foreach (var row in packageInspection.Resources)
            {
                if (!processedKeys.Contains(row.FormattedKey))
                {
                    otherResources.Add(row);
                }
            }
        }

        // Speculative automatic MeshToTexture linking is removed.
        // ResourceLinks remains empty unless explicit verified TGI material references exist.

        Ts3ObjectModelDecompositionResult? objectModelDecomposition = null;
        if (_decompositionService != null && packageInspection.IsSuccess)
        {
            objectModelDecomposition = _decompositionService.Decompose(packageInspection);
            if (objectModelDecomposition.Issues != null && objectModelDecomposition.Issues.Count > 0)
            {
                issues.AddRange(objectModelDecomposition.Issues);
            }

            ResolveDecompositionMeshCandidates(packageInspection, meshInspection, objectModelDecomposition, meshAssets, issues);
        }

        // Speculative automatic MeshToTexture linking is removed.
        // ResourceLinks remains empty unless explicit verified TGI material references exist.

        bool hasImportableMesh = meshAssets.Any(m =>
            m.ClassificationKind == MeshClassificationKind.KnownMesh &&
            (m.DetectedGameVersion == GameVersion.Sims3 || m.DetectedGameVersion == GameVersion.Sims4) &&
            m.RoleKind == MeshRoleKind.Geometry &&
            m.ResourceId.TypeId == Ts3GeomTypeId &&
            m.CanInspectCanonicalMesh);

        bool hasTextureCandidates = textureAssets.Any(t => t.CanExtractRawPayload);
        bool hasObjectModelResources = (objectModelDecomposition != null && objectModelDecomposition.TotalModelCount > 0) ||
            (packageInspection.Resources != null && packageInspection.Resources.Any(r => r.TypeId == TsModelTypeId || r.TypeId == TsModelLodTypeId));

        bool isSourceGraphReady = packageInspection.IsSuccess && hasImportableMesh;

        if (!hasImportableMesh)
        {
            if (hasObjectModelResources)
            {
                string decompositionMetadataInfo = (objectModelDecomposition != null && objectModelDecomposition.HasDecompositionMetadata)
                    ? " (decomposition metadata available)"
                    : string.Empty;

                issues.Add(new ConversionIssue(
                    "CONVG003",
                    $"No direct TS3 GEOM resource found; object model conversion requires MODL/MLOD decomposition{decompositionMetadataInfo}.",
                    ConversionIssueSeverity.Warning
                ));
            }
            else
            {
                issues.Add(new ConversionIssue(
                    "CONVG002",
                    "No importable TS3 GEOM mesh candidate found in source package.",
                    ConversionIssueSeverity.Warning
                ));
            }
        }

        meshAssets.Sort((a, b) => string.Compare(a.FormattedKey, b.FormattedKey, StringComparison.Ordinal));

        var catalogMetadata = _catalogMetadataReader.ReadCatalogMetadata(
            sourcePath,
            packageInspection.Resources ?? Array.Empty<PackageResourceRow>(),
            Path.GetFileNameWithoutExtension(sourcePath)
        );

        if (catalogMetadata.IsDefaultFallback)
        {
            issues.Add(new ConversionIssue(
                "CATL001",
                "Source package missing real TS3 catalog metadata; fallback catalog metadata (Price=100, Placement=Floor/Surface) applied.",
                ConversionIssueSeverity.Warning
            ));
        }

        return new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: sourcePath,
            TotalResourceCount: packageInspection.Resources?.Count ?? 0,
            MeshAssets: meshAssets.AsReadOnly(),
            TextureAssets: textureAssets.AsReadOnly(),
            ResourceLinks: links.AsReadOnly(),
            OtherResources: otherResources.AsReadOnly(),
            HasImportableMesh: hasImportableMesh,
            HasTextureCandidates: hasTextureCandidates,
            IsSourceGraphReady: isSourceGraphReady,
            Issues: issues.AsReadOnly(),
            ObjectModelDecomposition: objectModelDecomposition,
            CatalogMetadata: catalogMetadata
        );
    }

    private static void ResolveDecompositionMeshCandidates(
        PackageInspectionResult packageInspection,
        MeshInspectionResult meshInspection,
        Ts3ObjectModelDecompositionResult objectModelDecomposition,
        List<DecorativeObjectSourceMeshAsset> meshAssets,
        List<ConversionIssue> issues)
    {
        if (objectModelDecomposition == null || objectModelDecomposition.ModelMetadataResults == null || objectModelDecomposition.ModelMetadataResults.Count == 0)
        {
            return;
        }

        var pkgRowMap = new Dictionary<string, PackageResourceRow>(StringComparer.OrdinalIgnoreCase);
        if (packageInspection.Resources != null)
        {
            foreach (var r in packageInspection.Resources)
            {
                pkgRowMap[r.FormattedKey] = r;
            }
        }

        var meshRowMap = new Dictionary<string, MeshResourceRow>(StringComparer.OrdinalIgnoreCase);
        if (meshInspection.Rows != null)
        {
            foreach (var r in meshInspection.Rows)
            {
                meshRowMap[r.FormattedKey] = r;
            }
        }

        var existingKeys = new HashSet<string>(meshAssets.Select(m => m.FormattedKey), StringComparer.OrdinalIgnoreCase);
        var newlyResolved = new List<DecorativeObjectSourceMeshAsset>();
        var visitedModelIds = new HashSet<PackageResourceId>();

        var modelResourceIds = new HashSet<PackageResourceId>();
        foreach (var meta in objectModelDecomposition.ModelMetadataResults)
        {
            if (meta.ResourceId != null)
            {
                modelResourceIds.Add(meta.ResourceId);
            }
        }

        foreach (var meta in objectModelDecomposition.ModelMetadataResults)
        {
            if (!meta.IsSuccess)
            {
                continue;
            }

            if (meta.ResourceId != null && !visitedModelIds.Add(meta.ResourceId))
            {
                issues.Add(new ConversionIssue(
                    "CONVG006",
                    $"Cyclic or invalid model resource reference detected in decomposition for resource {meta.ResourceId.FormattedKey}.",
                    ConversionIssueSeverity.Warning
                ));
                continue;
            }

            if (meta.LodInfos != null)
            {
                foreach (var lod in meta.LodInfos)
                {
                    if (lod.AssociatedResourceId != null)
                    {
                        string lodKey = lod.AssociatedResourceId.FormattedKey;
                        if (!pkgRowMap.ContainsKey(lodKey))
                        {
                            issues.Add(new ConversionIssue(
                                "CONVG004",
                                $"Referenced TS3 MLOD resource {lodKey} not found in source package.",
                                ConversionIssueSeverity.Warning
                            ));
                        }
                        else if (meta.ResourceId != null && lod.AssociatedResourceId.Equals(meta.ResourceId))
                        {
                            issues.Add(new ConversionIssue(
                                "CONVG006",
                                $"Cyclic or invalid model resource reference detected in decomposition for resource {lodKey}.",
                                ConversionIssueSeverity.Warning
                            ));
                        }
                    }
                }
            }

            if (meta.GeometryReferences != null)
            {
                foreach (var geomRef in meta.GeometryReferences)
                {
                    if (geomRef.TargetResourceId == null)
                    {
                        continue;
                    }

                    var targetId = geomRef.TargetResourceId;
                    string formattedKey = targetId.FormattedKey;

                    if (modelResourceIds.Contains(targetId))
                    {
                        issues.Add(new ConversionIssue(
                            "CONVG006",
                            $"Cyclic or invalid model resource reference detected in decomposition for resource {formattedKey}.",
                            ConversionIssueSeverity.Warning
                        ));
                        continue;
                    }

                    if (targetId.TypeId == Ts3GeomTypeId)
                    {
                        if (!pkgRowMap.TryGetValue(formattedKey, out _))
                        {
                            issues.Add(new ConversionIssue(
                                "CONVG004",
                                $"Referenced TS3 GEOM resource {formattedKey} not found in source package.",
                                ConversionIssueSeverity.Warning
                            ));
                        }
                        else if (meshRowMap.TryGetValue(formattedKey, out var meshRow))
                        {
                            if (meshRow.ClassificationKind == MeshClassificationKind.KnownMesh &&
                                (meshRow.DetectedGameVersion == GameVersion.Sims3 || meshRow.DetectedGameVersion == GameVersion.Sims4) &&
                                meshRow.RoleKind == MeshRoleKind.Geometry &&
                                meshRow.Entry.Id.TypeId == Ts3GeomTypeId &&
                                meshRow.CanInspectCanonicalMesh)
                            {
                                if (existingKeys.Add(formattedKey))
                                {
                                    newlyResolved.Add(new DecorativeObjectSourceMeshAsset(
                                        FormattedKey: meshRow.FormattedKey,
                                        ResourceId: meshRow.Entry.Id,
                                        ClassificationKind: meshRow.ClassificationKind,
                                        RoleKind: meshRow.RoleKind,
                                        FormatName: meshRow.FormatName,
                                        DetectedGameVersion: meshRow.DetectedGameVersion,
                                        CanInspectCanonicalMesh: meshRow.CanInspectCanonicalMesh,
                                        VertexCount: meshRow.VertexCount,
                                        FaceCount: meshRow.FaceCount,
                                        BoneCount: meshRow.BoneCount,
                                        Issues: meshRow.Issues ?? Array.Empty<ConversionIssue>(),
                                        Entry: meshRow.Entry
                                    ));
                                }
                            }
                            else
                            {
                                issues.Add(new ConversionIssue(
                                    "CONVG005",
                                    $"Referenced TS3 GEOM resource {formattedKey} payload extraction or canonical mesh import failed.",
                                    ConversionIssueSeverity.Warning
                                ));
                            }
                        }
                        else
                        {
                            issues.Add(new ConversionIssue(
                                "CONVG005",
                                $"Referenced TS3 GEOM resource {formattedKey} payload extraction or canonical mesh import failed.",
                                ConversionIssueSeverity.Warning
                            ));
                        }
                    }
                }
            }
        }

        newlyResolved.Sort((a, b) => string.Compare(a.FormattedKey, b.FormattedKey, StringComparison.Ordinal));
        meshAssets.AddRange(newlyResolved);
    }

    private static DecorativeObjectSourceAssetGraph CreateFailureGraph(
        string sourcePath,
        string code,
        string message,
        IReadOnlyList<ConversionIssue>? existingIssues = null)
    {
        var issues = new List<ConversionIssue>();
        if (existingIssues != null)
        {
            issues.AddRange(existingIssues);
        }
        issues.Add(new ConversionIssue(code, message, ConversionIssueSeverity.Error));

        return new DecorativeObjectSourceAssetGraph(
            SourcePackagePath: sourcePath,
            TotalResourceCount: 0,
            MeshAssets: Array.Empty<DecorativeObjectSourceMeshAsset>(),
            TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            OtherResources: Array.Empty<PackageResourceRow>(),
            HasImportableMesh: false,
            HasTextureCandidates: false,
            IsSourceGraphReady: false,
            Issues: issues.AsReadOnly(),
            ObjectModelDecomposition: null
        );
    }
}
