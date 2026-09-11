using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;

namespace SimsConverter.Application.Tests;

public class Ts3UnknownResourceCatalogRegressionTests
{
    private class FakePackageInspectionService : Application.Contracts.IPackageInspectionService
    {
        public PackageInspectionResult ResultToReturn { get; set; } = new(true, "fake.package", null, Array.Empty<PackageResourceRow>(), Array.Empty<ConversionIssue>());
        public Task<PackageInspectionResult> InspectFileAsync(string filePath, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(ResultToReturn);
    }

    private class FakeMeshInspectionService : Application.Contracts.IMeshInspectionService
    {
        public MeshInspectionResult ResultToReturn { get; set; } = new(true, "fake.package", Array.Empty<MeshResourceRow>(), Array.Empty<ConversionIssue>());
        public Task<MeshInspectionResult> InspectPackageMeshesAsync(MeshInspectionRequest request, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(ResultToReturn);
        public MeshInspectionResult InspectPackageMeshes(PackageInspectionResult packageInspection, GameVersion gameVersionHint = GameVersion.Unknown) => ResultToReturn;
    }

    private class FakeTextureInspectionService : Application.Contracts.ITextureInspectionService
    {
        public TextureInspectionResult ResultToReturn { get; set; } = new(true, "fake.package", Array.Empty<TextureResourceRow>(), Array.Empty<ConversionIssue>());
        public Task<TextureInspectionResult> InspectPackageTexturesAsync(TextureInspectionRequest request, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(ResultToReturn);
        public TextureInspectionResult InspectPackageTextures(PackageInspectionResult packageInspection, GameVersion gameVersionHint = GameVersion.Unknown) => ResultToReturn;
    }

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public Ts3UnknownResourceCatalogRegressionTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }
    [Fact]
    public async Task EvaluateCapabilityAndCreatePlan_WhenPackageContainsUnknownAndUnsupportedCatalogTypes_EvaluatesHumanReadableWarningsAndFeasiblePlan()
    {
        // Arrange: Create PackageInspectionResult containing MODL, MLOD, GEOM, RMAT, RSLT + unsupported 0x736884F1, 0x03B4C61D, 0x033A1435
        var resources = new List<PackageResourceRow>
        {
            PackageResourceRow.FromEntry(new PackageResourceEntry(new Domain.Models.PackageResourceId(0x01661233, 0x00000000, 0x1000UL), 0, 100, 100, PackageCompressionKind.None, 0)),
            PackageResourceRow.FromEntry(new PackageResourceEntry(new Domain.Models.PackageResourceId(0x01D10F34, 0x00000000, 0x1001UL), 0, 100, 100, PackageCompressionKind.None, 0)),
            PackageResourceRow.FromEntry(new PackageResourceEntry(new Domain.Models.PackageResourceId(0x015A1849, 0x00000000, 0x1002UL), 0, 100, 100, PackageCompressionKind.None, 0)),
            PackageResourceRow.FromEntry(new PackageResourceEntry(new Domain.Models.PackageResourceId(0x2172D019, 0x00000000, 0x1003UL), 0, 100, 100, PackageCompressionKind.None, 0)),
            PackageResourceRow.FromEntry(new PackageResourceEntry(new Domain.Models.PackageResourceId(0xD3044521, 0x00000000, 0x1004UL), 0, 100, 100, PackageCompressionKind.None, 0)),
            PackageResourceRow.FromEntry(new PackageResourceEntry(new Domain.Models.PackageResourceId(0x736884F1, 0x00000000, 0x1005UL), 0, 100, 100, PackageCompressionKind.None, 0)), // Footprint
            PackageResourceRow.FromEntry(new PackageResourceEntry(new Domain.Models.PackageResourceId(0x03B4C61D, 0x00000000, 0x1006UL), 0, 100, 100, PackageCompressionKind.None, 0)), // RCOL Header
            PackageResourceRow.FromEntry(new PackageResourceEntry(new Domain.Models.PackageResourceId(0x033A1435, 0x00000000, 0x1007UL), 0, 100, 100, PackageCompressionKind.None, 0))  // Design Mode Preset
        };

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "sample_ts3.package",
            Header: null,
            Resources: resources,
            Issues: Array.Empty<ConversionIssue>()
        );

        var capabilityService = new DecorativeObjectConversionCapabilityService();

        // Act 1: Evaluate Capability Matrix directly
        var matrix = capabilityService.EvaluateCapability(packageResult, GameVersion.Sims3, GameVersion.Sims4);

        // Assert 1: Capability Matrix evaluation
        matrix.TotalResourcesAnalyzed.Should().Be(8);
        matrix.SupportedResourceCount.Should().Be(5, "MODL, MLOD, GEOM, RMAT, RSLT are supported.");
        matrix.UnsupportedResourceCount.Should().Be(3, "0x736884F1, 0x03B4C61D, 0x033A1435 are unsupported game-specific catalog types.");
        matrix.IsConversionFeasible.Should().BeTrue("Conversion feasibility is true.");

        // Assert 2: Human-readable CAPA001 warning messages
        matrix.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("Footprint (FTPT 0x736884F1)"));
        matrix.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("Model RCOL Header (0x03B4C61D)"));
        matrix.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("Design Mode Preset (0x033A1435)"));

        // Act 2: Verify CreateConversionPlanAsync integration
        string tempSourceFile = Path.Combine(Path.GetTempPath(), "ts3_capa_sample_" + Guid.NewGuid().ToString("N") + ".package");
        string tempTargetFile = Path.Combine(Path.GetTempPath(), "ts4_capa_target_" + Guid.NewGuid().ToString("N") + ".package");

        try
        {
            File.WriteAllBytes(tempSourceFile, new byte[100]);

            var fakePkgService = new FakePackageInspectionService { ResultToReturn = packageResult with { FilePath = tempSourceFile } };
            var entry = new PackageResourceEntry(new Domain.Models.PackageResourceId(0x015A1849, 0x00000000, 0x1002UL), 0, 100, 100, PackageCompressionKind.None, 0);
            var meshClassification = new MeshResourceClassification(
                entry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, Array.Empty<ConversionIssue>()
            );
            var meshRow = new MeshResourceRow(
                FormattedKey: entry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000001002",
                DataOffset: 0, CompressedSize: 100, DecompressedSize: 100, CompressionName: "None",
                ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM",
                DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
                VertexCount: 100, FaceCount: 50, BoneCount: 2, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
                Issues: Array.Empty<ConversionIssue>(), Entry: entry, Classification: meshClassification
            );

            var fakeMeshService = new FakeMeshInspectionService
            {
                ResultToReturn = new MeshInspectionResult(
                    IsSuccess: true,
                    PackageFilePath: tempSourceFile,
                    Rows: new[] { meshRow },
                    Issues: Array.Empty<ConversionIssue>()
                )
            };
            var fakeTexService = new FakeTextureInspectionService();

            var conversionService = new DecorativeObjectConversionService(fakePkgService, fakeMeshService, fakeTexService, capabilityService: capabilityService);

            var request = new DecorativeObjectConversionRequest(tempSourceFile, tempTargetFile, GameVersion.Sims4);
            var result = await conversionService.CreateConversionPlanAsync(request);

            // Assert 3: Plan feasibility & warning preservation
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue("Warnings do NOT block conversion plan feasibility.");
            result.Plan.Should().NotBeNull();
            result.Plan!.IsFeasible.Should().BeTrue();
            result.Plan.CapabilityMatrix.Should().NotBeNull();
            result.Plan.CapabilityMatrix!.SupportedResourceCount.Should().Be(5);
            result.Plan.CapabilityMatrix.UnsupportedResourceCount.Should().Be(3);

            result.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("Footprint (FTPT 0x736884F1)"));
            result.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("Model RCOL Header (0x03B4C61D)"));
            result.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("Design Mode Preset (0x033A1435)"));
        }
        finally
        {
            if (File.Exists(tempSourceFile)) File.Delete(tempSourceFile);
        }
    }
}
