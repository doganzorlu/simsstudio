using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Services;

namespace SimsConverter.Application.Services;

public class DecorativeObjectPackageWritePlanBuilder : IDecorativeObjectPackageWritePlanBuilder
{
    private readonly IPackageResourcePayloadReader _payloadReader;
    private readonly IDecorativeObjectTs4ResourceGenerator _ts4ResourceGenerator;
    private readonly IDecorativeObjectTs3ResourceGenerator _ts3ResourceGenerator;

    public DecorativeObjectPackageWritePlanBuilder(
        IPackageResourcePayloadReader? payloadReader = null,
        IDecorativeObjectTs4ResourceGenerator? ts4ResourceGenerator = null,
        IDecorativeObjectTs3ResourceGenerator? ts3ResourceGenerator = null)
    {
        _payloadReader = payloadReader ?? new PackageResourcePayloadReader();
        _ts4ResourceGenerator = ts4ResourceGenerator ?? new DecorativeObjectTs4ResourceGenerator();
        _ts3ResourceGenerator = ts3ResourceGenerator ?? new DecorativeObjectTs3ResourceGenerator();
    }

    public async Task<DecorativeObjectPackageWritePlan> BuildWritePlanAsync(
        DecorativeObjectConversionInputBundle bundle,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => BuildWritePlan(bundle), cancellationToken).ConfigureAwait(false);
    }

    public DecorativeObjectPackageWritePlan BuildWritePlan(DecorativeObjectConversionInputBundle bundle)
    {
        var issues = new List<ConversionIssue>();

        if (bundle == null)
        {
            issues.Add(new ConversionIssue("WRIT000", "Conversion input bundle is null.", ConversionIssueSeverity.Error));
            return CreateInvalidPlan(string.Empty, string.Empty, GameVersion.Sims4, issues);
        }

        if (bundle.Issues != null && bundle.Issues.Count > 0)
        {
            issues.AddRange(bundle.Issues);
        }

        bool hasObjectModel = bundle.ObjectModelDecomposition != null || (bundle.OtherResources != null && bundle.OtherResources.Any(r => r.TypeId == 0x01661233 || r.TypeId == 0x01D10F34));

        if (!bundle.IsBundleValid || bundle.MeshBundles == null || (bundle.MeshBundles.Count == 0 && !hasObjectModel))
        {
            issues.Add(new ConversionIssue("WRIT000", "Conversion input bundle is invalid or contains no mesh bundles or object model resources.", ConversionIssueSeverity.Error));
            return CreateInvalidPlan(bundle.SourcePackagePath, bundle.TargetOutputPath, bundle.TargetGameVersion, issues);
        }

        var plannedResources = new List<DecorativeObjectPackageWriteResourceEntry>();
        var addedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int meshCount = 0;
        int textureCount = 0;
        int rigCount = 0;
        int rsltCount = 0;
        int otherCount = 0;

        // 0. Catalog Object / Model / MLOD / Material Resource Generation according to TargetGameVersion
        IReadOnlyList<DecorativeObjectPackageWriteResourceEntry>? generatedList = null;
        IReadOnlyList<DecorativeObjectSourceResourceLink>? generatedVerifiedLinks = null;

        if (bundle.TargetGameVersion == GameVersion.Sims3)
        {
            var genResult = _ts3ResourceGenerator.GenerateResources(bundle);
            if (genResult.Issues != null && genResult.Issues.Count > 0)
            {
                issues.AddRange(genResult.Issues);
            }
            generatedList = genResult.GeneratedResources;
            generatedVerifiedLinks = genResult.VerifiedLinks;
        }
        else
        {
            var genResult = _ts4ResourceGenerator.GenerateResources(bundle);
            if (genResult.Issues != null && genResult.Issues.Count > 0)
            {
                issues.AddRange(genResult.Issues);
            }
            generatedList = genResult.GeneratedResources;
            generatedVerifiedLinks = genResult.VerifiedLinks;
        }

        if (generatedList != null)
        {
            foreach (var genRes in generatedList)
            {
                if (addedKeys.Add(genRes.FormattedKey))
                {
                    if (genRes.Payload.Count == 0)
                    {
                        issues.Add(new ConversionIssue(
                            "WRIT003",
                            $"Failed to extract complete payload for generated target resource {genRes.FormattedKey}.",
                            ConversionIssueSeverity.Error
                        ));
                        continue;
                    }

                    plannedResources.Add(genRes);
                    otherCount++;
                }
            }
        }

        // 1. Mesh Resources
        foreach (var meshBundle in bundle.MeshBundles)
        {
            if (addedKeys.Add(meshBundle.FormattedKey))
            {
                byte[] payload;
                if (bundle.TargetGameVersion == GameVersion.Sims4 && meshBundle.CanonicalMesh != null && meshBundle.CanonicalMesh.Vertices.Count > 0)
                {
                    var geomId = meshBundle.ResourceId;
                    var materialId = new PackageResourceId(SimsConverter.Domain.Constants.Ts4ResourceTypeIds.MaterialDefinition, 0, geomId.InstanceId);
                    payload = SimsConverter.Mesh.Services.Ts4GeomPayloadBuilder.BuildGeomPayload(geomId, materialId, meshBundle.CanonicalMesh);
                }
                else
                {
                    payload = (meshBundle.RawPayload ?? Array.Empty<byte>()).ToArray();
                }

                if (payload.Length == 0)
                {
                    issues.Add(new ConversionIssue(
                        "WRIT003",
                        $"Failed to extract complete payload for mesh resource {meshBundle.FormattedKey}.",
                        ConversionIssueSeverity.Error
                    ));
                    continue;
                }

                uint size = (uint)payload.Length;
                plannedResources.Add(new DecorativeObjectPackageWriteResourceEntry(
                    ResourceId: meshBundle.ResourceId,
                    FormattedKey: meshBundle.FormattedKey,
                    Payload: payload,
                    CompressionKind: PackageCompressionKind.None,
                    DecompressedSize: size,
                    CompressedSize: size
                ));
                meshCount++;
            }
        }

        // 2. Texture Resources
        if (bundle.TextureAssets != null)
        {
            foreach (var texAsset in bundle.TextureAssets)
            {
                bool alreadyGenerated = generatedList != null && generatedList.Any(g =>
                    g.ResourceId.InstanceId == texAsset.ResourceId.InstanceId &&
                    (g.ResourceId.TypeId == 0x3453CF95u || g.ResourceId.TypeId == 0x00B2D882u || g.ResourceId == texAsset.ResourceId));

                if (alreadyGenerated)
                {
                    textureCount++;
                    continue;
                }

                if (addedKeys.Add(texAsset.FormattedKey))
                {
                    IReadOnlyList<byte>? payload = texAsset.RawPayload;
                    if ((payload == null || payload.Count == 0) && texAsset.Entry != null && !string.IsNullOrEmpty(bundle.SourcePackagePath) && File.Exists(bundle.SourcePackagePath))
                    {
                        var pResult = _payloadReader.ReadPayload(bundle.SourcePackagePath, texAsset.Entry);
                        if (pResult.IsSuccess && pResult.Payload != null)
                        {
                            payload = Array.AsReadOnly(pResult.Payload.ToArray());
                        }
                    }

                    bool isGenuinelyZeroByte = texAsset.Entry != null && texAsset.Entry.DecompressedSize == 0 && texAsset.Entry.CompressedSize == 0;
                    if ((payload == null || payload.Count == 0) && !isGenuinelyZeroByte)
                    {
                        issues.Add(new ConversionIssue(
                            "WRIT003",
                            $"Failed to extract complete payload for texture resource {texAsset.FormattedKey}.",
                            ConversionIssueSeverity.Error
                        ));
                        continue;
                    }

                    var validPayload = payload ?? Array.Empty<byte>();
                    uint size = (uint)validPayload.Count;
                    plannedResources.Add(new DecorativeObjectPackageWriteResourceEntry(
                        ResourceId: texAsset.ResourceId,
                        FormattedKey: texAsset.FormattedKey,
                        Payload: validPayload,
                        CompressionKind: PackageCompressionKind.None,
                        DecompressedSize: size,
                        CompressedSize: size
                    ));
                    textureCount++;
                }
            }
        }

        // 3. RIG Resources
        if (bundle.RigResources != null)
        {
            foreach (var rigRow in bundle.RigResources)
            {
                bool alreadyGenerated = generatedList != null && generatedList.Any(g => g.ResourceId.TypeId == rigRow.TypeId);
                if (alreadyGenerated) continue;

                if (addedKeys.Add(rigRow.FormattedKey))
                {
                    var entry = rigRow.ToEntry();
                    IReadOnlyList<byte>? payload = null;
                    if (!string.IsNullOrEmpty(bundle.SourcePackagePath) && File.Exists(bundle.SourcePackagePath))
                    {
                        var pResult = _payloadReader.ReadPayload(bundle.SourcePackagePath, entry);
                        if (pResult.IsSuccess && pResult.Payload != null)
                        {
                            payload = Array.AsReadOnly(pResult.Payload.ToArray());
                        }
                    }

                    bool isGenuinelyZeroByte = entry.DecompressedSize == 0 && entry.CompressedSize == 0;
                    if ((payload == null || payload.Count == 0) && !isGenuinelyZeroByte)
                    {
                        issues.Add(new ConversionIssue(
                            "WRIT003",
                            $"Failed to extract complete payload for RIG resource {rigRow.FormattedKey}.",
                            ConversionIssueSeverity.Error
                        ));
                        continue;
                    }

                    var validPayload = payload ?? Array.Empty<byte>();
                    uint size = (uint)validPayload.Count;
                    plannedResources.Add(new DecorativeObjectPackageWriteResourceEntry(
                        ResourceId: entry.Id,
                        FormattedKey: rigRow.FormattedKey,
                        Payload: validPayload,
                        CompressionKind: PackageCompressionKind.None,
                        DecompressedSize: size,
                        CompressedSize: size
                    ));
                    rigCount++;
                }
            }
        }

        // 4. RSLT Resources
        if (bundle.RsltResources != null)
        {
            foreach (var rsltRow in bundle.RsltResources)
            {
                bool alreadyGenerated = generatedList != null && generatedList.Any(g => g.ResourceId.TypeId == rsltRow.TypeId);
                if (alreadyGenerated) continue;

                if (addedKeys.Add(rsltRow.FormattedKey))
                {
                    var entry = rsltRow.ToEntry();
                    IReadOnlyList<byte>? payload = null;
                    if (!string.IsNullOrEmpty(bundle.SourcePackagePath) && File.Exists(bundle.SourcePackagePath))
                    {
                        var pResult = _payloadReader.ReadPayload(bundle.SourcePackagePath, entry);
                        if (pResult.IsSuccess && pResult.Payload != null)
                        {
                            payload = Array.AsReadOnly(pResult.Payload.ToArray());
                        }
                    }

                    bool isGenuinelyZeroByte = entry.DecompressedSize == 0 && entry.CompressedSize == 0;
                    if ((payload == null || payload.Count == 0) && !isGenuinelyZeroByte)
                    {
                        issues.Add(new ConversionIssue(
                            "WRIT003",
                            $"Failed to extract complete payload for RSLT resource {rsltRow.FormattedKey}.",
                            ConversionIssueSeverity.Error
                        ));
                        continue;
                    }

                    var validPayload = payload ?? Array.Empty<byte>();
                    uint size = (uint)validPayload.Count;
                    plannedResources.Add(new DecorativeObjectPackageWriteResourceEntry(
                        ResourceId: entry.Id,
                        FormattedKey: rsltRow.FormattedKey,
                        Payload: validPayload,
                        CompressionKind: PackageCompressionKind.None,
                        DecompressedSize: size,
                        CompressedSize: size
                    ));
                    rsltCount++;
                }
            }
        }

        // 5. Pass-Through Whitelisted Resources (STBL, THUM, ICON)
        if (bundle.OtherResources != null)
        {
            foreach (var otherRow in bundle.OtherResources)
            {
                if (DecorativeObjectConversionCapabilityService.PassThroughTypeIds.Contains(otherRow.TypeId))
                {
                    if (addedKeys.Add(otherRow.FormattedKey))
                    {
                        var entry = otherRow.ToEntry();
                        IReadOnlyList<byte>? payload = null;
                        if (!string.IsNullOrEmpty(bundle.SourcePackagePath) && File.Exists(bundle.SourcePackagePath))
                        {
                            var pResult = _payloadReader.ReadPayload(bundle.SourcePackagePath, entry);
                            if (pResult.IsSuccess && pResult.Payload != null)
                            {
                                payload = Array.AsReadOnly(pResult.Payload.ToArray());
                            }
                        }

                        bool isGenuinelyZeroByte = entry.DecompressedSize == 0 && entry.CompressedSize == 0;
                        if ((payload == null || payload.Count == 0) && !isGenuinelyZeroByte)
                        {
                            issues.Add(new ConversionIssue(
                                "WRIT003",
                                $"Failed to extract complete payload for pass-through resource {otherRow.FormattedKey}.",
                                ConversionIssueSeverity.Error
                            ));
                            continue;
                        }

                        var validPayload = payload ?? Array.Empty<byte>();
                        uint size = (uint)validPayload.Count;
                        plannedResources.Add(new DecorativeObjectPackageWriteResourceEntry(
                            ResourceId: entry.Id,
                            FormattedKey: otherRow.FormattedKey,
                            Payload: validPayload,
                            CompressionKind: PackageCompressionKind.None,
                            DecompressedSize: size,
                            CompressedSize: size
                        ));
                        otherCount++;
                    }
                }
            }
        }

        plannedResources.Sort((a, b) => string.Compare(a.FormattedKey, b.FormattedKey, StringComparison.Ordinal));

        long totalPayloadBytes = plannedResources.Sum(r => (long)r.Payload.Count);
        int verifiedLinkCount = (bundle.ResourceLinks != null ? bundle.ResourceLinks.Count : 0) + (generatedVerifiedLinks != null ? generatedVerifiedLinks.Count : 0);

        int effectiveMeshCount = meshCount > 0 ? meshCount : (bundle.ObjectModelDecomposition?.MlodCount ?? 0);

        var report = new DecorativeObjectResourceSetReport(
            SourcePackagePath: bundle.SourcePackagePath ?? string.Empty,
            TargetOutputPath: bundle.TargetOutputPath ?? string.Empty,
            TotalResourceCount: plannedResources.Count,
            MeshCount: effectiveMeshCount,
            TextureCount: textureCount,
            RigCount: rigCount,
            RsltCount: rsltCount,
            OtherCount: otherCount,
            TotalPayloadBytes: totalPayloadBytes,
            VerifiedLinkCount: verifiedLinkCount,
            AssembledResources: plannedResources.AsReadOnly(),
            Issues: issues.AsReadOnly()
        );

        bool isPlanValid = !issues.Exists(i => i.Severity is ConversionIssueSeverity.Error or ConversionIssueSeverity.Fatal) && plannedResources.Count > 0;

        return new DecorativeObjectPackageWritePlan(
            SourcePackagePath: bundle.SourcePackagePath ?? string.Empty,
            TargetOutputPath: bundle.TargetOutputPath ?? string.Empty,
            TargetGameVersion: bundle.TargetGameVersion,
            PlannedResources: plannedResources.AsReadOnly(),
            IsPlanValid: isPlanValid,
            Issues: issues.AsReadOnly(),
            ResourceSetReport: report
        );
    }

    private static DecorativeObjectPackageWritePlan CreateInvalidPlan(
        string sourcePackagePath,
        string targetOutputPath,
        GameVersion targetGameVersion,
        List<ConversionIssue> issues)
    {
        var report = new DecorativeObjectResourceSetReport(
            SourcePackagePath: sourcePackagePath,
            TargetOutputPath: targetOutputPath,
            TotalResourceCount: 0,
            MeshCount: 0,
            TextureCount: 0,
            RigCount: 0,
            RsltCount: 0,
            OtherCount: 0,
            TotalPayloadBytes: 0,
            VerifiedLinkCount: 0,
            AssembledResources: Array.Empty<DecorativeObjectPackageWriteResourceEntry>(),
            Issues: issues.AsReadOnly()
        );

        return new DecorativeObjectPackageWritePlan(
            SourcePackagePath: sourcePackagePath,
            TargetOutputPath: targetOutputPath,
            TargetGameVersion: targetGameVersion,
            PlannedResources: Array.Empty<DecorativeObjectPackageWriteResourceEntry>(),
            IsPlanValid: false,
            Issues: issues.AsReadOnly(),
            ResourceSetReport: report
        );
    }
}
