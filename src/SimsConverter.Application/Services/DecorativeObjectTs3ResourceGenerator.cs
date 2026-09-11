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

public class DecorativeObjectTs3ResourceGenerator : IDecorativeObjectTs3ResourceGenerator
{
    private const uint Ts3DdsTypeId = 0x00B2D882u;
    private const uint Ts3GeomTypeId = 0x015A1849u;
    private const uint Ts3ModlTypeId = 0x01661233u;
    private const uint Ts3MlodTypeId = 0x01D10F34u;
    private const uint Ts3ObjdTypeId = 0x319E4F1Du;
    private const uint Ts3RigTypeId = 0x8EAF13DEu;
    private const uint Ts3RsltTypeId = 0xD3044521u;

    private readonly IDecorativeObjectTs4IdentityGenerator _identityGenerator;
    private readonly ITs4Rle2TextureDecoder _rle2Decoder;

    public DecorativeObjectTs3ResourceGenerator(
        IDecorativeObjectTs4IdentityGenerator? identityGenerator = null,
        ITs4Rle2TextureDecoder? rle2Decoder = null)
    {
        _identityGenerator = identityGenerator ?? new DecorativeObjectTs4IdentityGenerator();
        _rle2Decoder = rle2Decoder ?? new Ts4Rle2TextureDecoder();
    }

    public Ts3ResourceGenerationResult GenerateResources(DecorativeObjectConversionInputBundle bundle)
    {
        if (bundle == null) throw new ArgumentNullException(nameof(bundle));

        var generatedResources = new List<DecorativeObjectPackageWriteResourceEntry>();
        var verifiedLinks = new List<DecorativeObjectSourceResourceLink>();
        var issues = new List<ConversionIssue>();

        bool hasObjectModel = bundle.ObjectModelDecomposition != null || (bundle.OtherResources != null && bundle.OtherResources.Any(r => r.TypeId == 0x319E4F1D || r.TypeId == 0x01661233 || r.TypeId == 0x01D10F34));

        if ((bundle.MeshBundles == null || bundle.MeshBundles.Count == 0) && !hasObjectModel)
        {
            return new Ts3ResourceGenerationResult(
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
            : (firstModlRow != null ? new PackageResourceId(firstModlRow.TypeId, firstModlRow.GroupId, firstModlRow.InstanceId) : new PackageResourceId(0x319E4F1D, 0, 1));

        // Deterministic Identity Generation for TS3 target resources
        var modlId = _identityGenerator.MapResourceIdentity(primaryMeshId, Ts3ModlTypeId);
        var mlodL0Id = _identityGenerator.GenerateDeterministicResourceId(Ts3MlodTypeId, seedBase + "_TS3_MLOD_LOD0");
        var mlodL1Id = _identityGenerator.GenerateDeterministicResourceId(Ts3MlodTypeId, seedBase + "_TS3_MLOD_LOD1");

        // Verify & Convert Texture References to TS3 DDS Payloads
        var verifiedTextures = new List<DecorativeObjectSourceTextureAsset>();
        if (bundle.TextureAssets != null)
        {
            foreach (var tex in bundle.TextureAssets)
            {
                if (tex.FormattedKey.Contains("Heuristic", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ConversionIssue(
                        "IDEN001",
                        $"Unverified or heuristic texture reference '{tex.FormattedKey}' reported during TS3 identity assembly.",
                        ConversionIssueSeverity.Warning
                    ));
                }

                var ddsId = _identityGenerator.MapResourceIdentity(tex.ResourceId, Ts3DdsTypeId);
                byte[]? ddsPayload = null;

                if (tex.RawPayload != null && tex.RawPayload.Count > 0)
                {
                    byte[] rawBytes = tex.RawPayload.ToArray();
                    if (tex.ResourceId.TypeId == 0x3453CF95u || (rawBytes.Length >= 4 && rawBytes[0] == (byte)'R' && rawBytes[1] == (byte)'L' && rawBytes[2] == (byte)'E' && rawBytes[3] == (byte)'2'))
                    {
                        var rle2DecodeResult = _rle2Decoder.Decode(rawBytes, tex.FormattedKey);
                        if (rle2DecodeResult.IsSuccess && rle2DecodeResult.DecodedDdsPayload != null)
                        {
                            ddsPayload = rle2DecodeResult.DecodedDdsPayload;
                        }
                        else
                        {
                            if (rle2DecodeResult.Issues != null)
                            {
                                issues.AddRange(rle2DecodeResult.Issues);
                            }
                        }
                    }
                    else
                    {
                        ddsPayload = rawBytes;
                    }
                }

                if (ddsPayload != null && ddsPayload.Length > 0)
                {
                    generatedResources.Add(CreateResourceEntry(ddsId, ddsPayload));
                    verifiedTextures.Add(tex with { ResourceId = ddsId, FormattedKey = ddsId.FormattedKey, RawPayload = ddsPayload });
                }
                else
                {
                    verifiedTextures.Add(tex with { ResourceId = ddsId, FormattedKey = ddsId.FormattedKey });
                }
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
                        $"Unverified or heuristic material reference '{mesh.MaterialReferenceKey}' reported during TS3 identity assembly.",
                        ConversionIssueSeverity.Warning
                    ));
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
            return new Ts3ResourceGenerationResult(
                Array.Empty<DecorativeObjectPackageWriteResourceEntry>(),
                Array.Empty<DecorativeObjectSourceResourceLink>(),
                issues.AsReadOnly()
            );
        }

        var materialId = _identityGenerator.GenerateDeterministicResourceId(0x015A182Cu, seedBase + "_TS3_Material_0");

        // 1. Material Resource Payload
        var matPayload = BuildTs3MaterialPayload(materialId, verifiedTextures, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(materialId, matPayload));

        // 2. Generate TS3 MLOD LOD0 & LOD1 Payloads
        var mlodL0Payload = BuildTs3ModelLodPayload(mlodL0Id, 0, verifiedMeshBundles, materialId, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(mlodL0Id, mlodL0Payload));

        var mlodL1Payload = BuildTs3ModelLodPayload(mlodL1Id, 1, verifiedMeshBundles, materialId, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(mlodL1Id, mlodL1Payload));

        // 3. Generate TS3 MODL Payload
        var modlPayload = BuildTs3ModelPayload(modlId, mlodL0Id, mlodL1Id, verifiedLinks);
        generatedResources.Add(CreateResourceEntry(modlId, modlPayload));

        // 4. Generate TS3 OBJD Catalog Metadata Payload
        var objdId = _identityGenerator.MapResourceIdentity(primaryMeshId, Ts3ObjdTypeId);
        var objdPayload = BuildTs3ObjdPayload(bundle.CatalogMetadata);
        generatedResources.Add(CreateResourceEntry(objdId, objdPayload));

        // 5. Include RIG and RSLT Target Resources if none provided in bundle
        if (bundle.RigResources == null || bundle.RigResources.Count == 0)
        {
            var rigId = _identityGenerator.GenerateDeterministicResourceId(Ts3RigTypeId, seedBase + "_TS3_RIG");
            var rigPayload = BuildTs3RigPayload(rigId);
            generatedResources.Add(CreateResourceEntry(rigId, rigPayload));
        }

        if (bundle.RsltResources == null || bundle.RsltResources.Count == 0)
        {
            var rsltId = _identityGenerator.GenerateDeterministicResourceId(Ts3RsltTypeId, seedBase + "_TS3_RSLT");
            var rsltPayload = BuildTs3RsltPayload(rsltId);
            generatedResources.Add(CreateResourceEntry(rsltId, rsltPayload));
        }

        return new Ts3ResourceGenerationResult(
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

    private static byte[] BuildTs3MaterialPayload(PackageResourceId materialId, List<DecorativeObjectSourceTextureAsset> textures, List<DecorativeObjectSourceResourceLink> links)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

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

    private static byte[] BuildTs3ModelPayload(PackageResourceId modlId, PackageResourceId mlodL0Id, PackageResourceId mlodL1Id, List<DecorativeObjectSourceResourceLink> links)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        writer.Write(Encoding.ASCII.GetBytes("MODL"));
        writer.Write((uint)1); // Version 1
        writer.Write((uint)2); // 2 LODs: LOD0 and LOD1

        writer.Write(mlodL0Id.TypeId);
        writer.Write(mlodL0Id.GroupId);
        writer.Write(mlodL0Id.InstanceId);
        links.Add(new DecorativeObjectSourceResourceLink(modlId.FormattedKey, mlodL0Id.FormattedKey, "MODL_TO_MLOD_LOD0"));

        writer.Write(mlodL1Id.TypeId);
        writer.Write(mlodL1Id.GroupId);
        writer.Write(mlodL1Id.InstanceId);
        links.Add(new DecorativeObjectSourceResourceLink(modlId.FormattedKey, mlodL1Id.FormattedKey, "MODL_TO_MLOD_LOD1"));

        return ms.ToArray();
    }

    private static byte[] BuildTs3ModelLodPayload(PackageResourceId mlodId, uint lodIndex, List<DecorativeObjectMeshInputBundle> meshes, PackageResourceId materialId, List<DecorativeObjectSourceResourceLink> links)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        var targetMeshes = meshes.Where(m => m.AssociatedLodIndex == lodIndex).ToList();
        if (targetMeshes.Count == 0)
        {
            targetMeshes = meshes;
        }

        writer.Write(Encoding.ASCII.GetBytes("MLOD"));
        writer.Write((uint)1); // Version 1
        writer.Write(lodIndex);
        writer.Write((uint)targetMeshes.Count);

        foreach (var mesh in targetMeshes)
        {
            writer.Write(mesh.ResourceId.TypeId);
            writer.Write(mesh.ResourceId.GroupId);
            writer.Write(mesh.ResourceId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(mlodId.FormattedKey, mesh.ResourceId.FormattedKey, $"MLOD_LOD{lodIndex}_TO_GEOM"));
        }

        if (targetMeshes.Count > 0)
        {
            writer.Write(materialId.TypeId);
            writer.Write(materialId.GroupId);
            writer.Write(materialId.InstanceId);
            links.Add(new DecorativeObjectSourceResourceLink(mlodId.FormattedKey, materialId.FormattedKey, "MLOD_TO_MATERIAL"));
        }

        return ms.ToArray();
    }

    private static byte[] BuildTs3RigPayload(PackageResourceId rigId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        writer.Write(Encoding.ASCII.GetBytes("RIG_"));
        writer.Write((uint)1); // Version 1
        writer.Write((uint)0); // Bone Count 0
        return ms.ToArray();
    }

    private static byte[] BuildTs3RsltPayload(PackageResourceId rsltId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        writer.Write(Encoding.ASCII.GetBytes("RSLT"));
        writer.Write((uint)1); // Version 1
        writer.Write((uint)0); // Slot Count 0
        return ms.ToArray();
    }

    private static byte[] BuildTs3ObjdPayload(ObjectCatalogMetadata? catalogMetadata)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        writer.Write(Encoding.ASCII.GetBytes("OBJD"));
        writer.Write((uint)1); // Version 1
        writer.Write(catalogMetadata?.Price ?? 100u);
        writer.Write(catalogMetadata?.CatalogGroup ?? 0u);
        writer.Write(catalogMetadata?.PlacementFlags ?? 0x00000001u);
        writer.Write(catalogMetadata?.FootprintHash ?? 0u);
        return ms.ToArray();
    }
}
