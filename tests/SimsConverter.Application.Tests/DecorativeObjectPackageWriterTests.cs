using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Services;
using Xunit;

namespace SimsConverter.Application.Tests;

public class DecorativeObjectPackageWriterTests
{
    private static PackageResourceEntry CreateEntry(PackageResourceId id) =>
        new PackageResourceEntry(id, DataOffset: 1024, CompressedSize: 256, DecompressedSize: 512, CompressionKind: PackageCompressionKind.None, CompressionFlags: 0);

    private static CanonicalMesh CreateMesh() =>
        new CanonicalMesh(
            "TestMesh",
            new[] { new CanonicalVertex(new MeshVector3(0, 0, 0), new MeshVector3(0, 1, 0), null, new MeshVector2(0, 0), null, null) },
            new[] { new CanonicalFace(0, 0, 0) },
            null,
            CanonicalCoordinateSystem.RightHandedYUp,
            GameVersion.Sims3,
            null
        );

    [Fact]
    public void BuildWritePlan_NullOrInvalidInputBundle_ReturnsInvalidWritePlanWithWRIT000()
    {
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();

        var plan = planBuilder.BuildWritePlan(null!);

        plan.IsPlanValid.Should().BeFalse();
        plan.Issues.Should().Contain(i => i.Code == "WRIT000");
    }

    [Fact]
    public void BuildWritePlan_ValidInputBundle_CreatesValidWritePlan_SortsResourcesDeterministically()
    {
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();

        var idB = new PackageResourceId(0x015A1849, 0, 2);
        var idA = new PackageResourceId(0x015A1849, 0, 1);

        var meshBundleB = new DecorativeObjectMeshInputBundle(idB, idB.FormattedKey, CreateMesh(), new byte[] { 0x01, 0x02 });
        var meshBundleA = new DecorativeObjectMeshInputBundle(idA, idA.FormattedKey, CreateMesh(), new byte[] { 0x03, 0x04 });

        var bundle = new DecorativeObjectConversionInputBundle(
            SourcePackagePath: "source.package",
            TargetOutputPath: "output.package",
            TargetGameVersion: GameVersion.Sims4,
            MeshBundles: new[] { meshBundleB, meshBundleA },
            TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ObjectModelDecomposition: null,
            RigResources: Array.Empty<PackageResourceRow>(),
            RsltResources: Array.Empty<PackageResourceRow>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            IsBundleValid: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var plan = planBuilder.BuildWritePlan(bundle);

        plan.IsPlanValid.Should().BeTrue();
        plan.PlannedResources.Should().HaveCount(7, "Includes 2 mesh bundles + 5 generated TS4 object resources (COBJ, MODL, MLODx2, Material).");
        string.Compare(plan.PlannedResources[0].FormattedKey, plan.PlannedResources[1].FormattedKey, StringComparison.Ordinal).Should().BeLessThan(0);
    }

    private static PackageResourceEntry CreateZeroByteEntry(PackageResourceId id) =>
        new PackageResourceEntry(id, DataOffset: 0, CompressedSize: 0, DecompressedSize: 0, CompressionKind: PackageCompressionKind.None, CompressionFlags: 0);

    [Fact]
    public void BuildWritePlan_AggregatesFullResourceSet_AndGeneratesAssemblyReport()
    {
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();

        var meshId = new PackageResourceId(0x015A1849, 0, 1);
        var texId = new PackageResourceId(0x00B2D882, 0, 2);
        var rigId = new PackageResourceId(0x8EAF13DE, 0, 3);
        var rsltId = new PackageResourceId(0xD3044521, 0, 4);

        var meshBundle = new DecorativeObjectMeshInputBundle(meshId, meshId.FormattedKey, CreateMesh(), new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var texAsset = new DecorativeObjectSourceTextureAsset(texId.FormattedKey, texId, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS", true, Array.Empty<ConversionIssue>(), CreateEntry(texId), new byte[] { 0x44, 0x44, 0x53, 0x20 });

        var rigRow = PackageResourceRow.FromEntry(CreateZeroByteEntry(rigId));
        var rsltRow = PackageResourceRow.FromEntry(CreateZeroByteEntry(rsltId));

        var bundle = new DecorativeObjectConversionInputBundle(
            SourcePackagePath: "source.package",
            TargetOutputPath: "output.package",
            TargetGameVersion: GameVersion.Sims4,
            MeshBundles: new[] { meshBundle },
            TextureAssets: new[] { texAsset },
            ObjectModelDecomposition: null,
            RigResources: new[] { rigRow },
            RsltResources: new[] { rsltRow },
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            IsBundleValid: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var plan = planBuilder.BuildWritePlan(bundle);

        plan.IsPlanValid.Should().BeTrue();
        plan.PlannedResources.Should().HaveCount(9, "Includes 4 input resources + 5 generated TS4 object resources.");
        plan.ResourceSetReport.Should().NotBeNull();
        plan.ResourceSetReport!.MeshCount.Should().Be(1);
        plan.ResourceSetReport.TextureCount.Should().Be(1);
        plan.ResourceSetReport.RigCount.Should().Be(1);
        plan.ResourceSetReport.RsltCount.Should().Be(1);
        plan.ResourceSetReport.TotalResourceCount.Should().Be(9);
    }

    [Fact]
    public void BuildWritePlan_WhenTexturePayloadExtractionFails_EmitsWRIT003_AndSetsIsPlanValidFalse()
    {
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();

        var meshId = new PackageResourceId(0x015A1849, 0, 1);
        var texId = new PackageResourceId(0x00B2D882, 0, 2);

        var meshBundle = new DecorativeObjectMeshInputBundle(meshId, meshId.FormattedKey, CreateMesh(), new byte[] { 0x01, 0x02 });
        // Texture asset with non-zero entry size but null/empty payload and non-existent file
        var texAssetWithMissingPayload = new DecorativeObjectSourceTextureAsset(
            texId.FormattedKey, texId, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS", true,
            Array.Empty<ConversionIssue>(), CreateEntry(texId), RawPayload: null
        );

        var bundle = new DecorativeObjectConversionInputBundle(
            SourcePackagePath: "non_existent_source.package",
            TargetOutputPath: "output.package",
            TargetGameVersion: GameVersion.Sims4,
            MeshBundles: new[] { meshBundle },
            TextureAssets: new[] { texAssetWithMissingPayload },
            ObjectModelDecomposition: null,
            RigResources: Array.Empty<PackageResourceRow>(),
            RsltResources: Array.Empty<PackageResourceRow>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            IsBundleValid: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var plan = planBuilder.BuildWritePlan(bundle);

        plan.IsPlanValid.Should().BeFalse();
        plan.Issues.Should().Contain(i => i.Code == "WRIT003");
        plan.PlannedResources.Should().NotContain(r => r.FormattedKey == texId.FormattedKey, "Resource with missing payload must be rejected from planned resources.");
    }

    [Fact]
    public void WritePackage_NullOrInvalidWritePlan_ReturnsFailureWithWRIT000()
    {
        var writer = new DecorativeObjectPackageWriter();

        var result = writer.WritePackage(null!);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "WRIT000");
    }

    [Fact]
    public void WritePackage_WhenSourceAndTargetPathsAreIdentical_EnforcesOverwriteGuardWithWRIT001()
    {
        var writer = new DecorativeObjectPackageWriter();

        string samePath = Path.Combine(Path.GetTempPath(), "same_file.package");

        var plan = new DecorativeObjectPackageWritePlan(
            SourcePackagePath: samePath,
            TargetOutputPath: samePath,
            TargetGameVersion: GameVersion.Sims4,
            PlannedResources: new[]
            {
                new DecorativeObjectPackageWriteResourceEntry(new PackageResourceId(1, 1, 1), "0x00000001:0x00000001:0x0000000000000001", new byte[] { 0x01 }, PackageCompressionKind.None, 1, 1)
            },
            IsPlanValid: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var result = writer.WritePackage(plan);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "WRIT001");
        File.Exists(samePath).Should().BeFalse("Source file must not be overwritten or created on path collision.");
    }

    [Fact]
    public void WritePackage_ValidWritePlan_WritesPackageFileAtomically_ParsesCleanlyWithDbpfPackageParser()
    {
        var writer = new DecorativeObjectPackageWriter();
        var parser = new DbpfPackageParser();

        string tempDir = Path.Combine(Path.GetTempPath(), "sims_conv_writer_test_" + Guid.NewGuid().ToString("N"));
        string sourcePath = Path.Combine(tempDir, "source.package");
        string targetPath = Path.Combine(tempDir, "target_ts4.package");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(sourcePath, "dummy_source");

            var res1 = new DecorativeObjectPackageWriteResourceEntry(
                ResourceId: new PackageResourceId(0x015A1849, 0, 1),
                FormattedKey: "0x015A1849:0x00000000:0x0000000000000001",
                Payload: new byte[] { 0x47, 0x45, 0x4F, 0x4D, 0x01, 0x02 },
                CompressionKind: PackageCompressionKind.None,
                DecompressedSize: 6,
                CompressedSize: 6
            );

            var res2 = new DecorativeObjectPackageWriteResourceEntry(
                ResourceId: new PackageResourceId(0x00B2D882, 0, 2),
                FormattedKey: "0x00B2D882:0x00000000:0x0000000000000002",
                Payload: new byte[] { 0x44, 0x44, 0x53, 0x20 },
                CompressionKind: PackageCompressionKind.None,
                DecompressedSize: 4,
                CompressedSize: 4
            );

            var plan = new DecorativeObjectPackageWritePlan(
                SourcePackagePath: sourcePath,
                TargetOutputPath: targetPath,
                TargetGameVersion: GameVersion.Sims4,
                PlannedResources: new[] { res1, res2 },
                IsPlanValid: true,
                Issues: Array.Empty<ConversionIssue>()
            );

            var result = writer.WritePackage(plan);

            result.IsSuccess.Should().BeTrue();
            result.TargetOutputPath.Should().Be(Path.GetFullPath(targetPath));
            result.ResourceCount.Should().Be(2);
            File.Exists(targetPath).Should().BeTrue("Target TS4 package file must exist after atomic move.");

            byte[] generatedBytes = File.ReadAllBytes(targetPath);
            var parseResult = parser.Parse(generatedBytes);

            parseResult.IsSuccess.Should().BeTrue("Written TS4 .package file must parse cleanly via DBPF parser.");
            parseResult.Header.Should().NotBeNull();
            parseResult.Header.MajorVersion.Should().Be(2, "TS4 package output header major version must be 2.");
            parseResult.Header.IndexEntryCount.Should().Be(2);
            parseResult.Entries.Should().HaveCount(2);

            parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x015A1849 && e.Id.InstanceId == 1 && e.CompressedSize == 6);
            parseResult.Entries.Should().Contain(e => e.Id.TypeId == 0x00B2D882 && e.Id.InstanceId == 2 && e.CompressedSize == 4);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildWritePlan_GeneratesTs4ObjectCatalog_Model_Mlod_AndMaterialResources_WithVerifiedTgiLinks()
    {
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();

        var meshId = new PackageResourceId(0x015A1849, 0, 100);
        var texId = new PackageResourceId(0x00B2D882, 0, 200);

        var meshBundle = new DecorativeObjectMeshInputBundle(meshId, meshId.FormattedKey, CreateMesh(), new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var texAsset = new DecorativeObjectSourceTextureAsset(texId.FormattedKey, texId, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS", true, Array.Empty<ConversionIssue>(), CreateEntry(texId), new byte[] { 0x44, 0x44, 0x53, 0x20 });

        var bundle = new DecorativeObjectConversionInputBundle(
            SourcePackagePath: "source_onyx.package",
            TargetOutputPath: "output_ts4.package",
            TargetGameVersion: GameVersion.Sims4,
            MeshBundles: new[] { meshBundle },
            TextureAssets: new[] { texAsset },
            ObjectModelDecomposition: null,
            RigResources: Array.Empty<PackageResourceRow>(),
            RsltResources: Array.Empty<PackageResourceRow>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            IsBundleValid: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var plan = planBuilder.BuildWritePlan(bundle);

        plan.IsPlanValid.Should().BeTrue();
        // COBJ (0x319E4F1D), MODL (0x01661233), MLOD LOD0 (0x01D10F34), MLOD LOD1 (0x01D10F34), Material (0x2172D019), Mesh (0x015A1849), Texture (0x00B2D882) = 7 resources
        plan.PlannedResources.Should().Contain(r => r.ResourceId.TypeId == 0x319E4F1D, "Catalog Object (COBJ) must be generated.");
        plan.PlannedResources.Should().Contain(r => r.ResourceId.TypeId == 0x01661233, "Model (MODL) must be generated.");
        plan.PlannedResources.Should().Contain(r => r.ResourceId.TypeId == 0x01D10F34, "Model LOD (MLOD) must be generated.");
        plan.PlannedResources.Should().Contain(r => r.ResourceId.TypeId == 0x2172D019, "Material Definition (RMAT) must be generated.");

        plan.ResourceSetReport.Should().NotBeNull();
        plan.ResourceSetReport!.VerifiedLinkCount.Should().BeGreaterThan(0, "Verified TGI links between catalog, model, MLOD, material, and textures must be registered.");
    }

    [Fact]
    public void BuildWritePlan_WhenUnverifiedOrHeuristicReferencePresent_RejectsReference_AndEmitsIDEN001Warning()
    {
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();

        var meshId = new PackageResourceId(0x015A1849, 0, 100);
        var unverifiedTexId = new PackageResourceId(0x00B2D882, 0, 999);

        var meshBundle = new DecorativeObjectMeshInputBundle(meshId, meshId.FormattedKey, CreateMesh(), new byte[] { 0x01, 0x02 });
        var unverifiedTexAsset = new DecorativeObjectSourceTextureAsset(
            "0x00B2D882:0x00000000:0x00000000000003E7_Heuristic", unverifiedTexId, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS", true,
            Array.Empty<ConversionIssue>(), CreateEntry(unverifiedTexId), new byte[] { 0x44, 0x44, 0x53, 0x20 }
        );

        var bundle = new DecorativeObjectConversionInputBundle(
            SourcePackagePath: "source_heuristic.package",
            TargetOutputPath: "output_ts4.package",
            TargetGameVersion: GameVersion.Sims4,
            MeshBundles: new[] { meshBundle },
            TextureAssets: new[] { unverifiedTexAsset },
            ObjectModelDecomposition: null,
            RigResources: Array.Empty<PackageResourceRow>(),
            RsltResources: Array.Empty<PackageResourceRow>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            IsBundleValid: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var plan = planBuilder.BuildWritePlan(bundle);

        plan.IsPlanValid.Should().BeTrue();
        plan.Issues.Should().Contain(i => i.Code == "IDEN001", "Unverified or heuristic texture reference must produce IDEN001 warning issue.");
    }

    [Fact]
    public void BuildWritePlan_WhenMeshHasHeuristicMaterialReference_RejectsMeshFromAssembly_AndGeneratesNoUnverifiedMlodLinks()
    {
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();

        var meshId1 = new PackageResourceId(0x015A1849, 0, 100);
        var meshId2 = new PackageResourceId(0x015A1849, 0, 200);

        var validMeshBundle = new DecorativeObjectMeshInputBundle(meshId1, meshId1.FormattedKey, CreateMesh(), new byte[] { 0x01, 0x02 }, MaterialReferenceKey: "VerifiedMaterialKey");
        var unverifiedMeshBundle = new DecorativeObjectMeshInputBundle(meshId2, meshId2.FormattedKey, CreateMesh(), new byte[] { 0x03, 0x04 }, MaterialReferenceKey: "HeuristicMaterialKey_Unverified");

        var bundle = new DecorativeObjectConversionInputBundle(
            SourcePackagePath: "source_heuristic_mesh.package",
            TargetOutputPath: "output_ts4.package",
            TargetGameVersion: GameVersion.Sims4,
            MeshBundles: new[] { validMeshBundle, unverifiedMeshBundle },
            TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ObjectModelDecomposition: null,
            RigResources: Array.Empty<PackageResourceRow>(),
            RsltResources: Array.Empty<PackageResourceRow>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            IsBundleValid: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var plan = planBuilder.BuildWritePlan(bundle);

        plan.IsPlanValid.Should().BeTrue();
        plan.Issues.Should().Contain(i => i.Code == "IDEN001", "Unverified material reference must produce IDEN001 warning issue.");

        // Assert report links do NOT contain any MLOD link to unverifiedMeshBundle
        plan.ResourceSetReport.Should().NotBeNull();
        var assembledReport = plan.ResourceSetReport!;

        var genResult = new DecorativeObjectTs4ResourceGenerator().GenerateResources(bundle);
        genResult.VerifiedLinks.Should().NotContain(l => l.TargetKey == unverifiedMeshBundle.FormattedKey, "Unverified mesh must NOT be linked from MLOD.");
        genResult.VerifiedLinks.Should().Contain(l => l.TargetKey == validMeshBundle.FormattedKey, "Verified mesh MUST be linked from MLOD.");
    }

    [Fact]
    public void BuildWritePlan_WhenAllMeshesHaveHeuristicMaterialReferences_RejectsResourceGeneration_EmitsIDEN002_AndSetsIsPlanValidFalse()
    {
        var planBuilder = new DecorativeObjectPackageWritePlanBuilder();

        var unverifiedMeshId = new PackageResourceId(0x015A1849, 0, 100);
        var unverifiedMeshBundle = new DecorativeObjectMeshInputBundle(
            unverifiedMeshId, unverifiedMeshId.FormattedKey, CreateMesh(), new byte[] { 0x01, 0x02 }, MaterialReferenceKey: "HeuristicMaterialKey_Unverified"
        );

        var bundle = new DecorativeObjectConversionInputBundle(
            SourcePackagePath: "source_all_heuristic.package",
            TargetOutputPath: "output_ts4.package",
            TargetGameVersion: GameVersion.Sims4,
            MeshBundles: new[] { unverifiedMeshBundle },
            TextureAssets: Array.Empty<DecorativeObjectSourceTextureAsset>(),
            ObjectModelDecomposition: null,
            RigResources: Array.Empty<PackageResourceRow>(),
            RsltResources: Array.Empty<PackageResourceRow>(),
            ResourceLinks: Array.Empty<DecorativeObjectSourceResourceLink>(),
            IsBundleValid: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        var plan = planBuilder.BuildWritePlan(bundle);

        plan.IsPlanValid.Should().BeFalse("Plan must be invalid when no verified mesh bundles remain after filtering.");
        plan.Issues.Should().Contain(i => i.Code == "IDEN002", "Empty verified mesh set must emit IDEN002 error issue.");

        plan.PlannedResources.Should().NotContain(r => r.ResourceId.TypeId == 0x319E4F1D, "Empty COBJ resource must not be generated.");
        plan.PlannedResources.Should().NotContain(r => r.ResourceId.TypeId == 0x01661233, "Empty MODL resource must not be generated.");
        plan.PlannedResources.Should().NotContain(r => r.ResourceId.TypeId == 0x01D10F34, "Empty MLOD resource must not be generated.");
        plan.PlannedResources.Should().NotContain(r => r.ResourceId.TypeId == 0x2172D019, "Empty Material resource must not be generated.");
    }

    [Fact]
    public void DecorativeObjectTs4IdentityGenerator_MapResourceIdentity_And_GenerateDeterministicResourceId_AreDeterministic()
    {
        var generator = new DecorativeObjectTs4IdentityGenerator();

        var sourceId = new PackageResourceId(0x015A1849, 0x80000000, 0x123456789ABCDEF0);
        var mapped1 = generator.MapResourceIdentity(sourceId, 0x319E4F1D);
        var mapped2 = generator.MapResourceIdentity(sourceId, 0x319E4F1D);

        mapped1.Should().Be(mapped2, "Mapping resource identity must be purely deterministic.");
        mapped1.TypeId.Should().Be(0x319E4F1D);
        mapped1.GroupId.Should().Be(0x80000000);
        mapped1.InstanceId.Should().Be(0x123456789ABCDEF0);

        var seedId1 = generator.GenerateDeterministicResourceId(0x01661233, "OnyxSeed");
        var seedId2 = generator.GenerateDeterministicResourceId(0x01661233, "OnyxSeed");

        seedId1.Should().Be(seedId2, "Generating resource identity from seed must be purely deterministic.");
        seedId1.TypeId.Should().Be(0x01661233);
    }
}
