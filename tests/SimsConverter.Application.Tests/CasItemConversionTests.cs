using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;

namespace SimsConverter.Application.Tests;

public class CasItemConversionTests
{
    [Fact]
    public void ClassifyPackage_GivenCasPackage_ReturnsCasPartCategory()
    {
        // Arrange
        var classifier = new PackageItemClassifier();
        var resources = new[]
        {
            PackageResourceRow.FromEntry(new PackageResourceEntry(new PackageResourceId(Ts4ResourceTypeIds.CasPartTS3, 0, 1), 0, 100, 100, PackageCompressionKind.None, 0)),
            PackageResourceRow.FromEntry(new PackageResourceEntry(new PackageResourceId(Ts4ResourceTypeIds.Geom, 0, 2), 0, 500, 500, PackageCompressionKind.None, 0))
        };

        // Act
        var result = classifier.ClassifyPackage("cas.package", resources);

        // Assert
        result.MainCategory.Should().Be(PackageItemCategory.CasPart);
        result.Items.Should().HaveCount(1);
        result.Items[0].Category.Should().Be(PackageItemCategory.CasPart);
    }

    [Fact]
    public void Ts4CasPartPayloadBuilder_BuildsValidCaspPayload()
    {
        // Arrange
        var builder = new Ts4CasPartPayloadBuilder();
        var caspId = new PackageResourceId(Ts4ResourceTypeIds.CasPartTS4, 0, 1);
        var geomId = new PackageResourceId(Ts4ResourceTypeIds.Geom, 0, 2);
        var texId = new PackageResourceId(Ts4ResourceTypeIds.Rle2Texture, 0, 3);
        var meta = new CasPartMetadata(0x30, 0x01, 0x03, "TestCasPart", "Key1");

        // Act
        byte[] payload = builder.BuildCasPartPayload(caspId, geomId, texId, meta);

        // Assert
        payload.Should().NotBeNull();
        payload.Length.Should().BeGreaterThanOrEqualTo(52);
        Encoding.ASCII.GetString(payload, 0, 4).Should().Be("CASP");
    }

    [Fact]
    public void CasBoneRigRemapper_RemapsSkeletonBones()
    {
        // Arrange
        var remapper = new CasBoneRigRemapper();
        var vertex = new CanonicalVertex(
            position: new MeshVector3(0, 1, 0),
            normal: new MeshVector3(0, 1, 0),
            tangent: null,
            uv0: new MeshVector2(0.5f, 0.5f),
            uv1: null,
            boneWeights: new[] { new CanonicalBoneWeight((int)0x5C808E5Cu, 1.0f) }
        );
        var mesh = new CanonicalMesh(
            "TestMesh",
            new[] { vertex },
            Array.Empty<CanonicalFace>(),
            Array.Empty<CanonicalMaterialSlot>(),
            CanonicalCoordinateSystem.RightHandedYUp,
            GameVersion.Sims3,
            Array.Empty<ConversionIssue>()
        );

        // Act
        var remapped = remapper.RemapSkeletonBones(mesh);

        // Assert
        remapped.Should().NotBeNull();
        remapped.Vertices[0].BoneWeights![0].BoneIndex.Should().Be(0); // Mapped to TS4 Pelvis/Root
    }

    [SkippableFact]
    public async Task ConvertCasPackageAsync_WithRealUserCasPackage_ValidatesDeterminismGraphAndPayloads()
    {
        // Arrange
        string sourcePath = "/Users/dogan/Downloads/CARVER_F_body_theevangeline_DEF.package";
        if (!File.Exists(sourcePath))
        {
            Skip.If(true, $"[SKIPPED] Real CAS fixture not found at path '{sourcePath}'. Test skipped cleanly.");
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "cas_det_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string targetPath1 = Path.Combine(tempDir, "converted_cas_1.package");
        string targetPath2 = Path.Combine(tempDir, "converted_cas_2.package");

        try
        {
            var dbpfParser = new DbpfPackageParser();
            var pkgService = new PackageInspectionService(dbpfParser);
            var meshClassifier = new MeshResourceClassifier();
            var validator = new CanonicalMeshValidator();
            var payloadReader = new PackageResourcePayloadReader();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var meshService = new MeshInspectionService(pkgService, meshClassifier, ts3Importer, ts4Importer, payloadReader);
            var texClassifier = new TextureResourceClassifier();
            var texService = new TextureInspectionService(pkgService, texClassifier);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);

            var convService = new DecorativeObjectConversionService(pkgService, meshService, texService, payloadVerifier: payloadVerifier, dbpfParser: dbpfParser);

            // Act - Run 1
            var req1 = new DecorativeObjectConversionRequest(sourcePath, targetPath1, GameVersion.Sims4);
            var res1 = await convService.ExecuteConversionAsync(req1);

            // Assert - Run 1 Execution & Post-Write Graph Validation
            string errorDetails = string.Join("; ", res1.Issues.Select(i => $"[{i.Code}] {i.Severity}: {i.Message}"));
            res1.IsSuccess.Should().BeTrue($"Real CAS package conversion must succeed. Issues: {errorDetails}");
            File.Exists(targetPath1).Should().BeTrue();

            var parse1 = await dbpfParser.ParseFileAsync(targetPath1);
            parse1.IsSuccess.Should().BeTrue();
            parse1.Entries.Should().Contain(e => e.Id.TypeId == Ts4ResourceTypeIds.CasPartTS4, "Output package must contain TS4 CASP resource 0x034B5D85.");
            parse1.Entries.Should().Contain(e => e.Id.TypeId == Ts4ResourceTypeIds.Geom, "Output package must contain converted GEOM resources.");

            var verifyResult = await payloadVerifier.VerifyPackagePayloadsAsync(targetPath1, parse1);
            verifyResult.IsSuccess.Should().BeTrue("Written TS4 CAS package must pass payload compatibility verification.");
            verifyResult.VerifiedTgiLinkCount.Should().BeGreaterThanOrEqualTo(2, "CASP -> GEOM and CASP -> Texture TGI graph links must be verified.");

            // Act - Run 2 for Determinism Verification
            var req2 = new DecorativeObjectConversionRequest(sourcePath, targetPath2, GameVersion.Sims4);
            var res2 = await convService.ExecuteConversionAsync(req2);

            res2.IsSuccess.Should().BeTrue();
            byte[] bytes1 = await File.ReadAllBytesAsync(targetPath1);
            byte[] bytes2 = await File.ReadAllBytesAsync(targetPath2);

            bytes1.SequenceEqual(bytes2).Should().BeTrue("CAS package conversion across multiple runs for the same input must be 100% byte-for-byte deterministic.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ConvertCasPackageAsync_OnFailure_PreservesExistingTargetPackage()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "cas_rollback_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string existingTarget = Path.Combine(tempDir, "existing_target.package");
            byte[] originalBytes = Encoding.UTF8.GetBytes("PRESERVED_TARGET_PACKAGE_PAYLOAD");
            await File.WriteAllBytesAsync(existingTarget, originalBytes);

            string invalidSource = Path.Combine(tempDir, "non_existent_source.package");

            var dbpfParser = new DbpfPackageParser();
            var pkgService = new PackageInspectionService(dbpfParser);
            var meshClassifier = new MeshResourceClassifier();
            var validator = new CanonicalMeshValidator();
            var payloadReader = new PackageResourcePayloadReader();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var meshService = new MeshInspectionService(pkgService, meshClassifier, ts3Importer, ts4Importer, payloadReader);
            var texClassifier = new TextureResourceClassifier();
            var texService = new TextureInspectionService(pkgService, texClassifier);
            var convService = new DecorativeObjectConversionService(pkgService, meshService, texService);

            var req = new DecorativeObjectConversionRequest(invalidSource, existingTarget, GameVersion.Sims4);

            // Act
            var res = await convService.ExecuteConversionAsync(req);

            // Assert
            res.IsSuccess.Should().BeFalse();
            File.Exists(existingTarget).Should().BeTrue();

            byte[] currentBytes = await File.ReadAllBytesAsync(existingTarget);
            currentBytes.SequenceEqual(originalBytes).Should().BeTrue("Conversion failure must retain pre-existing target file untouched (atomic rollback).");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
