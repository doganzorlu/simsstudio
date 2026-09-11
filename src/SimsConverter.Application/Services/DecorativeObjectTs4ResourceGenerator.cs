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

namespace SimsConverter.Application.Services;

public class DecorativeObjectTs4ResourceGenerator : IDecorativeObjectTs4ResourceGenerator
{
    private readonly IDecorativeObjectTs4IdentityGenerator _identityGenerator;

    public DecorativeObjectTs4ResourceGenerator(IDecorativeObjectTs4IdentityGenerator? identityGenerator = null)
    {
        _identityGenerator = identityGenerator ?? new DecorativeObjectTs4IdentityGenerator();
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

        string seedBase = !string.IsNullOrEmpty(bundle.SourcePackagePath)
            ? Path.GetFileNameWithoutExtension(bundle.SourcePackagePath)
            : (bundle.MeshBundles != null && bundle.MeshBundles.Count > 0 ? bundle.MeshBundles[0].FormattedKey : "DecomposedObject");

        PackageResourceRow? firstModlRow = bundle.ObjectModelDecomposition?.ModlResources?.FirstOrDefault()
            ?? bundle.OtherResources?.FirstOrDefault(r => r.TypeId == 0x01661233 || r.TypeId == 0x319E4F1D);

        var primaryMeshId = bundle.MeshBundles != null && bundle.MeshBundles.Count > 0
            ? bundle.MeshBundles[0].ResourceId
            : (firstModlRow != null ? new PackageResourceId(firstModlRow.TypeId, firstModlRow.GroupId, firstModlRow.InstanceId) : new PackageResourceId(0x01661233, 0, 1));

        // Deterministic Identity Generation
        var cobjId = _identityGenerator.MapResourceIdentity(primaryMeshId, Ts4ResourceTypeIds.CatalogObject);
        var modlId = _identityGenerator.MapResourceIdentity(primaryMeshId, Ts4ResourceTypeIds.Model);
        var mlodL0Id = _identityGenerator.GenerateDeterministicResourceId(Ts4ResourceTypeIds.ModelLod, seedBase + "_MLOD_LOD0");
        var mlodL1Id = _identityGenerator.GenerateDeterministicResourceId(Ts4ResourceTypeIds.ModelLod, seedBase + "_MLOD_LOD1");
        var materialId = _identityGenerator.GenerateDeterministicResourceId(Ts4ResourceTypeIds.MaterialDefinition, seedBase + "_Material_0");

        // Verify Texture References
        var verifiedTextures = new List<DecorativeObjectSourceTextureAsset>();
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

                verifiedTextures.Add(tex);
            }
        }

        // Verify Material / Mesh References
        var verifiedMeshBundles = new List<DecorativeObjectMeshInputBundle>();
        if (bundle.MeshBundles != null)
        {
            foreach (var mesh in bundle.MeshBundles)
            {
                if (!string.IsNullOrEmpty(mesh.MaterialReferenceKey) && mesh.MaterialReferenceKey.Contains("Heuristic", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ConversionIssue(
                        "IDEN001",
                        $"Unverified or heuristic material reference '{mesh.MaterialReferenceKey}' was rejected during TS4 identity assembly.",
                        ConversionIssueSeverity.Warning
                    ));
                    continue;
                }
                verifiedMeshBundles.Add(mesh);
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
        var matPayload = BuildMaterialPayload(materialId, verifiedTextures, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(materialId, matPayload));

        // 2. MLOD LOD0 & LOD1 Resource Payloads
        var mlodL0Payload = BuildModelLodPayload(mlodL0Id, 0, verifiedMeshBundles, materialId, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(mlodL0Id, mlodL0Payload));

        var mlodL1Payload = BuildModelLodPayload(mlodL1Id, 1, verifiedMeshBundles, materialId, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(mlodL1Id, mlodL1Payload));

        // 3. MODL Resource Payload
        var modlPayload = BuildModelPayload(modlId, mlodL0Id, mlodL1Id, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(modlId, modlPayload));

        // 4. COBJ Catalog Object Resource Payload
        var cobjPayload = BuildCatalogObjectPayload(cobjId, modlId, verifiedLinks);
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

    private static byte[] BuildCatalogObjectPayload(PackageResourceId cobjId, PackageResourceId modlId, List<DecorativeObjectSourceResourceLink> links)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // Header magic: 'COBJ'
        writer.Write(Encoding.ASCII.GetBytes("COBJ"));
        writer.Write((uint)1); // Version 1

        // Reference to MODL
        writer.Write(modlId.TypeId);
        writer.Write(modlId.GroupId);
        writer.Write(modlId.InstanceId);

        links.Add(new DecorativeObjectSourceResourceLink(cobjId.FormattedKey, modlId.FormattedKey, "COBJ_To_MODL"));
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

        // Header magic: 'MLOD'
        writer.Write(Encoding.ASCII.GetBytes("MLOD"));
        writer.Write((uint)1); // Version 1
        writer.Write(lodIndex);
        writer.Write((uint)meshes.Count);

        foreach (var mesh in meshes)
        {
            writer.Write(mesh.ResourceId.TypeId);
            writer.Write(mesh.ResourceId.GroupId);
            writer.Write(mesh.ResourceId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(mlodId.FormattedKey, mesh.FormattedKey, $"MLOD_To_GEOM_LOD{lodIndex}"));
        }

        // Material reference (only if verified meshes exist)
        if (meshes.Count > 0)
        {
            writer.Write(materialId.TypeId);
            writer.Write(materialId.GroupId);
            writer.Write(materialId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(mlodId.FormattedKey, materialId.FormattedKey, "MLOD_To_Material"));
        }

        return ms.ToArray();
    }

    private static byte[] BuildMaterialPayload(PackageResourceId materialId, List<DecorativeObjectSourceTextureAsset> textures, List<DecorativeObjectSourceResourceLink> links)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // Header magic: 'RMAT'
        writer.Write(Encoding.ASCII.GetBytes("RMAT"));
        writer.Write((uint)1); // Version 1
        writer.Write((uint)textures.Count);

        foreach (var tex in textures)
        {
            writer.Write(tex.ResourceId.TypeId);
            writer.Write(tex.ResourceId.GroupId);
            writer.Write(tex.ResourceId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(materialId.FormattedKey, tex.FormattedKey, "Material_To_Texture"));
        }

        return ms.ToArray();
    }
}
