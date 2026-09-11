using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

using SimsConverter.Textures.Contracts;
using SimsConverter.Textures.Services;

namespace SimsConverter.Application.Services;

public class DecorativeObjectTs4ResourceGenerator : IDecorativeObjectTs4ResourceGenerator
{
    private readonly IDecorativeObjectTs4IdentityGenerator _identityGenerator;
    private readonly ITs4Rle2TexturePayloadBuilder _rle2PayloadBuilder;

    public DecorativeObjectTs4ResourceGenerator(
        IDecorativeObjectTs4IdentityGenerator? identityGenerator = null,
        ITs4Rle2TexturePayloadBuilder? rle2PayloadBuilder = null)
    {
        _identityGenerator = identityGenerator ?? new DecorativeObjectTs4IdentityGenerator();
        _rle2PayloadBuilder = rle2PayloadBuilder ?? new Ts4Rle2TexturePayloadBuilder();
    }

    public Ts4ResourceGenerationResult GenerateResources(DecorativeObjectConversionInputBundle bundle)
    {
        if (bundle == null) throw new ArgumentNullException(nameof(bundle));

        var generatedResources = new List<DecorativeObjectPackageWriteResourceEntry>();
        var verifiedLinks = new List<DecorativeObjectSourceResourceLink>();
        var issues = new List<ConversionIssue>();

        bool hasObjectModel = bundle.ObjectModelDecomposition != null || (bundle.OtherResources != null && bundle.OtherResources.Any(r => r.TypeId == 0x01661233 || r.TypeId == 0x01D10F34));

        if ((bundle.MeshBundles == null || bundle.MeshBundles.Count == 0) && !hasObjectModel)
        {
            return new Ts4ResourceGenerationResult(
                Array.Empty<DecorativeObjectPackageWriteResourceEntry>(),
                Array.Empty<DecorativeObjectSourceResourceLink>(),
                issues.AsReadOnly()
            );
        }

        string seedBase = bundle.MeshBundles != null && bundle.MeshBundles.Count > 0
            ? bundle.MeshBundles[0].ResourceId.InstanceId.ToString("X16")
            : (!string.IsNullOrEmpty(bundle.SourcePackagePath) ? Path.GetFileNameWithoutExtension(bundle.SourcePackagePath) : "DecomposedObject");

        PackageResourceRow? firstModlRow = bundle.ObjectModelDecomposition?.ModlResources?.FirstOrDefault()
            ?? bundle.OtherResources?.FirstOrDefault(r => r.TypeId == 0x01661233 || r.TypeId == 0x319E4F1D);

        var primaryMeshId = bundle.MeshBundles != null && bundle.MeshBundles.Count > 0
            ? bundle.MeshBundles[0].ResourceId
            : (firstModlRow != null ? new PackageResourceId(firstModlRow.TypeId, firstModlRow.GroupId, firstModlRow.InstanceId) : new PackageResourceId(0x01661233, 0, 1));

        // Deterministic Identity Generation
        var cobjId = _identityGenerator.MapResourceIdentity(primaryMeshId, Ts4ResourceTypeIds.CatalogObject);
        var objdId = _identityGenerator.MapResourceIdentity(primaryMeshId, Ts4ResourceTypeIds.ObjectDefinition);
        var modlId = _identityGenerator.MapResourceIdentity(primaryMeshId, Ts4ResourceTypeIds.Model);
        var mlodL0Id = _identityGenerator.GenerateDeterministicResourceId(Ts4ResourceTypeIds.ModelLod, seedBase + "_MLOD_LOD0");
        var mlodL1Id = _identityGenerator.GenerateDeterministicResourceId(Ts4ResourceTypeIds.ModelLod, seedBase + "_MLOD_LOD1");
        var materialId = _identityGenerator.GenerateDeterministicResourceId(Ts4ResourceTypeIds.MaterialDefinition, seedBase + "_Material_0");

        PackageResourceId? rigId = bundle.RigResources != null && bundle.RigResources.Count > 0
            ? new PackageResourceId(bundle.RigResources[0].TypeId, bundle.RigResources[0].GroupId, bundle.RigResources[0].InstanceId)
            : null;

        PackageResourceId? rsltId = bundle.RsltResources != null && bundle.RsltResources.Count > 0
            ? new PackageResourceId(bundle.RsltResources[0].TypeId, bundle.RsltResources[0].GroupId, bundle.RsltResources[0].InstanceId)
            : null;

        // Verify & Convert Texture References to TS4 RLE2 Payloads
        var convertedTextureIds = new List<PackageResourceId>();
        if (bundle.TextureAssets != null)
        {
            foreach (var tex in bundle.TextureAssets)
            {
                if (tex.FormattedKey.Contains("Heuristic", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ConversionIssue(
                        "IDEN001",
                        $"Unverified or heuristic texture reference '{tex.FormattedKey}' was rejected during TS4 identity assembly.",
                        ConversionIssueSeverity.Warning
                    ));
                    continue;
                }

                // Map TS3 DDS identity (0x00B2D882) to TS4 RLE2 identity (0x3453CF95)
                var rle2Id = _identityGenerator.MapResourceIdentity(tex.ResourceId, Ts4ResourceTypeIds.Rle2Texture);
                convertedTextureIds.Add(rle2Id);

                if (tex.RawPayload != null && tex.RawPayload.Count > 0)
                {
                    byte[] ddsBytes = tex.RawPayload.ToArray();
                    var rle2Result = _rle2PayloadBuilder.BuildPayload(ddsBytes, tex.FormattedKey);

                    if (rle2Result.IsSuccess && rle2Result.Payload != null)
                    {
                        generatedResources.Add(CreateResourceEntry(rle2Id, rle2Result.Payload));
                    }
                    else
                    {
                        issues.Add(new ConversionIssue(
                            "TEXR005",
                            $"RLE2 texture payload building failed for texture asset '{tex.FormattedKey}'.",
                            ConversionIssueSeverity.Warning
                        ));
                        if (rle2Result.Issues != null)
                        {
                            issues.AddRange(rle2Result.Issues);
                        }
                    }
                }
            }
        }

        // Verify Material / Mesh References
        var verifiedMeshBundles = new List<DecorativeObjectMeshInputBundle>();
        if (bundle.MeshBundles != null)
        {
            foreach (var mesh in bundle.MeshBundles)
            {
                if (!string.IsNullOrEmpty(mesh.MaterialReferenceKey) && mesh.MaterialReferenceKey.Contains("Unverified", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ConversionIssue(
                        "IDEN001",
                        $"Unverified or heuristic material reference '{mesh.MaterialReferenceKey}' reported during TS4 identity assembly.",
                        ConversionIssueSeverity.Warning
                    ));
                }
                else
                {
                    verifiedMeshBundles.Add(mesh);
                }
            }
        }

        if (bundle.MeshBundles != null && bundle.MeshBundles.Count > 0 && verifiedMeshBundles.Count == 0)
        {
            issues.Add(new ConversionIssue(
                "IDEN002",
                "No verified mesh bundles remaining after unverified material reference filtering.",
                ConversionIssueSeverity.Error
            ));
            return new Ts4ResourceGenerationResult(
                Array.Empty<DecorativeObjectPackageWriteResourceEntry>(),
                Array.Empty<DecorativeObjectSourceResourceLink>(),
                issues.AsReadOnly()
            );
        }

        // 1. Material Resource Payload (RMAT)
        var matPayload = BuildMaterialPayload(materialId, convertedTextureIds, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(materialId, matPayload));

        // 2. MLOD LOD0 & LOD1 Resource Payloads
        var lod0Meshes = verifiedMeshBundles.Where(m => !m.AssociatedLodIndex.HasValue || m.AssociatedLodIndex.Value == 0).ToList();
        var lod1Meshes = verifiedMeshBundles.Where(m => !m.AssociatedLodIndex.HasValue || m.AssociatedLodIndex.Value == 1).ToList();
        if (lod1Meshes.Count == 0 && lod0Meshes.Count > 0) lod1Meshes = lod0Meshes;

        var mlodL0Payload = BuildModelLodPayload(mlodL0Id, 0, lod0Meshes, materialId, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(mlodL0Id, mlodL0Payload));

        var mlodL1Payload = BuildModelLodPayload(mlodL1Id, 1, lod1Meshes, materialId, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(mlodL1Id, mlodL1Payload));

        // 3. MODL Resource Payload
        var modlPayload = BuildModelPayload(modlId, mlodL0Id, mlodL1Id, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(modlId, modlPayload));

        // 4. OBJD Object Definition Resource Payload
        var objdPayload = BuildObjectDefinitionPayload(objdId, modlId, rigId, rsltId, verifiedLinks, bundle.CatalogMetadata);
        generatedResources.Add(CreateResourceEntry(objdId, objdPayload));

        // 5. COBJ Catalog Object Resource Payload
        var cobjPayload = BuildCatalogObjectPayload(cobjId, objdId, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(cobjId, cobjPayload));

        return new Ts4ResourceGenerationResult(
            generatedResources.AsReadOnly(),
            verifiedLinks.AsReadOnly(),
            issues.AsReadOnly()
        );
    }

    private static DecorativeObjectPackageWriteResourceEntry CreateResourceEntry(PackageResourceId id, byte[] payload)
    {
        uint size = (uint)payload.Length;
        return new DecorativeObjectPackageWriteResourceEntry(
            ResourceId: id,
            FormattedKey: id.FormattedKey,
            Payload: payload,
            CompressionKind: PackageCompressionKind.None,
            DecompressedSize: size,
            CompressedSize: size
        );
    }

    private static byte[] BuildCatalogObjectPayload(PackageResourceId cobjId, PackageResourceId objdId, List<DecorativeObjectSourceResourceLink> links)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // Header magic: 'COBJ'
        writer.Write(Encoding.ASCII.GetBytes("COBJ"));
        writer.Write((uint)1); // Version 1

        // Reference to OBJD
        writer.Write(objdId.TypeId);
        writer.Write(objdId.GroupId);
        writer.Write(objdId.InstanceId);

        links.Add(new DecorativeObjectSourceResourceLink(cobjId.FormattedKey, objdId.FormattedKey, "COBJ_To_OBJD"));
        return ms.ToArray();
    }

    private static byte[] BuildObjectDefinitionPayload(
        PackageResourceId objdId,
        PackageResourceId modlId,
        PackageResourceId? rigId,
        PackageResourceId? rsltId,
        List<DecorativeObjectSourceResourceLink> links,
        ObjectCatalogMetadata? catalogMetadata = null)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // Header magic: 'OBJD'
        writer.Write(Encoding.ASCII.GetBytes("OBJD"));
        writer.Write((uint)1); // Version 1

        // Reference to MODL
        writer.Write(modlId.TypeId);
        writer.Write(modlId.GroupId);
        writer.Write(modlId.InstanceId);
        links.Add(new DecorativeObjectSourceResourceLink(objdId.FormattedKey, modlId.FormattedKey, "OBJD_To_MODL"));

        // Reference to RIG
        if (rigId != null && rigId.TypeId != 0)
        {
            writer.Write(rigId.TypeId);
            writer.Write(rigId.GroupId);
            writer.Write(rigId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(objdId.FormattedKey, rigId.FormattedKey, "OBJD_To_RIG"));
        }
        else
        {
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((ulong)0);
        }

        // Reference to RSLT
        if (rsltId != null && rsltId.TypeId != 0)
        {
            writer.Write(rsltId.TypeId);
            writer.Write(rsltId.GroupId);
            writer.Write(rsltId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(objdId.FormattedKey, rsltId.FormattedKey, "OBJD_To_RSLT"));
        }
        else
        {
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((ulong)0);
        }

        // Placement flags & Footprint metadata
        uint placementFlags = catalogMetadata?.PlacementFlags ?? 0x00000001;
        uint footprintHash = catalogMetadata?.FootprintHash ?? 0x00000000;
        uint price = catalogMetadata?.Price ?? 100;
        uint catalogGroup = catalogMetadata?.CatalogGroup ?? 0;

        writer.Write(placementFlags); // PlacementFlags
        writer.Write(footprintHash);  // FootprintHash

        // Price & Catalog metadata
        writer.Write(price);          // Price Simoleons
        writer.Write(catalogGroup);   // CatalogGroup

        return ms.ToArray();
    }

    private static byte[] BuildModelPayload(PackageResourceId modlId, PackageResourceId mlodL0Id, PackageResourceId mlodL1Id, List<DecorativeObjectSourceResourceLink> links)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // Header magic: 'MODL'
        writer.Write(Encoding.ASCII.GetBytes("MODL"));
        writer.Write((uint)1); // Version 1
        writer.Write((uint)2); // 2 LOD count

        // Reference to MLOD LOD0
        writer.Write(mlodL0Id.TypeId);
        writer.Write(mlodL0Id.GroupId);
        writer.Write(mlodL0Id.InstanceId);
        links.Add(new DecorativeObjectSourceResourceLink(modlId.FormattedKey, mlodL0Id.FormattedKey, "MODL_To_MLOD_LOD0"));

        // Reference to MLOD LOD1
        writer.Write(mlodL1Id.TypeId);
        writer.Write(mlodL1Id.GroupId);
        writer.Write(mlodL1Id.InstanceId);
        links.Add(new DecorativeObjectSourceResourceLink(modlId.FormattedKey, mlodL1Id.FormattedKey, "MODL_To_MLOD_LOD1"));

        return ms.ToArray();
    }

    private static byte[] BuildModelLodPayload(
        PackageResourceId mlodId,
        uint lodIndex,
        List<DecorativeObjectMeshInputBundle> meshes,
        PackageResourceId materialId,
        List<DecorativeObjectSourceResourceLink> links)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        var targetMeshes = meshes.Where(m => m.AssociatedLodIndex == lodIndex).ToList();
        if (targetMeshes.Count == 0)
        {
            targetMeshes = meshes;
        }

        // Header magic: 'MLOD'
        writer.Write(Encoding.ASCII.GetBytes("MLOD"));
        writer.Write((uint)1); // Version 1
        writer.Write(lodIndex);
        writer.Write((uint)targetMeshes.Count);

        foreach (var mesh in targetMeshes)
        {
            writer.Write(mesh.ResourceId.TypeId);
            writer.Write(mesh.ResourceId.GroupId);
            writer.Write(mesh.ResourceId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(mlodId.FormattedKey, mesh.FormattedKey, $"MLOD_To_GEOM_LOD{lodIndex}"));
        }

        // Material reference (only if verified meshes exist)
        if (targetMeshes.Count > 0)
        {
            writer.Write(materialId.TypeId);
            writer.Write(materialId.GroupId);
            writer.Write(materialId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(mlodId.FormattedKey, materialId.FormattedKey, "MLOD_To_Material"));
        }

        return ms.ToArray();
    }

    private static byte[] BuildMaterialPayload(PackageResourceId materialId, List<PackageResourceId> textureIds, List<DecorativeObjectSourceResourceLink> links)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // Header magic: 'RMAT'
        writer.Write(Encoding.ASCII.GetBytes("RMAT"));
        writer.Write((uint)1); // Version 1
        writer.Write((uint)textureIds.Count);

        foreach (var texTargetId in textureIds)
        {
            writer.Write(texTargetId.TypeId);
            writer.Write(texTargetId.GroupId);
            writer.Write(texTargetId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(materialId.FormattedKey, texTargetId.FormattedKey, "Material_To_Texture"));
        }

        return ms.ToArray();
    }
}
