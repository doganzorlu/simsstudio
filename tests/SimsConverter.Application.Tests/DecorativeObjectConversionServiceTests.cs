using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using Xunit;

namespace SimsConverter.Application.Tests;

public class DecorativeObjectConversionServiceTests
{
    private class FakePackageInspectionService : IPackageInspectionService
    {
        public PackageInspectionResult ResultToReturn { get; set; } = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "fake.package",
            Header: null,
            Resources: Array.Empty<PackageResourceRow>(),
            Issues: Array.Empty<ConversionIssue>()
        );

        public Task<PackageInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private class FakeMeshInspectionService : IMeshInspectionService
    {
        public MeshInspectionResult ResultToReturn { get; set; } = new MeshInspectionResult(
            IsSuccess: true,
            PackageFilePath: "fake.package",
            Rows: Array.Empty<MeshResourceRow>(),
            Issues: Array.Empty<ConversionIssue>()
        );

        public Task<MeshInspectionResult> InspectPackageMeshesAsync(MeshInspectionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }

        public MeshInspectionResult InspectPackageMeshes(PackageInspectionResult packageInspection, GameVersion gameVersionHint = GameVersion.Unknown)
        {
            return ResultToReturn;
        }
    }

    private class FakeTextureInspectionService : ITextureInspectionService
    {
        public TextureInspectionResult ResultToReturn { get; set; } = new TextureInspectionResult(
            IsSuccess: true,
            PackageFilePath: "fake.package",
            Rows: Array.Empty<TextureResourceRow>(),
            Issues: Array.Empty<ConversionIssue>()
        );

        public Task<TextureInspectionResult> InspectPackageTexturesAsync(TextureInspectionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }

        public TextureInspectionResult InspectPackageTextures(PackageInspectionResult packageInspection, GameVersion gameVersionHint = GameVersion.Unknown)
        {
            return ResultToReturn;
        }
    }

    [Fact]
    public async Task CreateConversionPlanAsync_NullRequest_ReturnsFailureCONVA000()
    {
        var pkgService = new FakePackageInspectionService();
        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var service = new DecorativeObjectConversionService(pkgService, meshService, texService);

        var result = await service.CreateConversionPlanAsync(null!);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "CONVA000");
    }

    [Fact]
    public async Task CreateConversionPlanAsync_MissingSourcePackagePath_ReturnsFailureCONVA001()
    {
        var pkgService = new FakePackageInspectionService();
        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var service = new DecorativeObjectConversionService(pkgService, meshService, texService);

        var request = new DecorativeObjectConversionRequest(SourcePackagePath: "", TargetOutputPath: "out.package");
        var result = await service.CreateConversionPlanAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "CONVA001");
    }

    [Fact]
    public async Task CreateConversionPlanAsync_NonExistentSourceFile_ReturnsFailureCONVA001()
    {
        var pkgService = new FakePackageInspectionService();
        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var service = new DecorativeObjectConversionService(pkgService, meshService, texService);

        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".package");
        var request = new DecorativeObjectConversionRequest(SourcePackagePath: nonExistentPath, TargetOutputPath: "out.package");
        var result = await service.CreateConversionPlanAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "CONVA001");
    }

    [Fact]
    public async Task CreateConversionPlanAsync_EmptyTargetOutputPath_ReturnsFailureCONVA002()
    {
        var pkgService = new FakePackageInspectionService();
        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var service = new DecorativeObjectConversionService(pkgService, meshService, texService);

        var tempSource = Path.GetTempFileName();
        try
        {
            var request = new DecorativeObjectConversionRequest(SourcePackagePath: tempSource, TargetOutputPath: "  ");
            var result = await service.CreateConversionPlanAsync(request);

            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle(i => i.Code == "CONVA002");
        }
        finally
        {
            if (File.Exists(tempSource)) File.Delete(tempSource);
        }
    }

    [Fact]
    public async Task CreateConversionPlanAsync_SameSourceAndTargetPath_ReturnsFailureCONVA003()
    {
        var pkgService = new FakePackageInspectionService();
        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var service = new DecorativeObjectConversionService(pkgService, meshService, texService);

        var tempSource = Path.GetTempFileName();
        try
        {
            var request = new DecorativeObjectConversionRequest(SourcePackagePath: tempSource, TargetOutputPath: tempSource);
            var result = await service.CreateConversionPlanAsync(request);

            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle(i => i.Code == "CONVA003");
        }
        finally
        {
            if (File.Exists(tempSource)) File.Delete(tempSource);
        }
    }

    [Fact]
    public async Task CreateConversionPlanAsync_PackageInspectionFailure_ReturnsFailureCONVA004()
    {
        var pkgService = new FakePackageInspectionService
        {
            ResultToReturn = PackageInspectionResult.Failure("corrupt.package", "PKGA001", "Corrupt DBPF container.")
        };
        var meshService = new FakeMeshInspectionService();
        var texService = new FakeTextureInspectionService();
        var service = new DecorativeObjectConversionService(pkgService, meshService, texService);

        var tempSource = Path.GetTempFileName();
        try
        {
            var request = new DecorativeObjectConversionRequest(SourcePackagePath: tempSource, TargetOutputPath: tempSource + ".out");
            var result = await service.CreateConversionPlanAsync(request);

            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().Contain(i => i.Code == "CONVA004");
        }
        finally
        {
            if (File.Exists(tempSource)) File.Delete(tempSource);
        }
    }

    [Fact]
    public async Task CreateConversionPlanAsync_ValidRequest_ProducesPlanWithNotImplementedWriterStep_AndDoesNotWriteFile()
    {
        var tempSource = Path.GetTempFileName();
        var tempTarget = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".package");

        try
        {
            var entry = new PackageResourceEntry(new PackageResourceId(0x015A1849, 0, 1), 128, 100, 200, PackageCompressionKind.Zlib, 0);
            var pkgRow = PackageResourceRow.FromEntry(entry);

            var pkgService = new FakePackageInspectionService
            {
                ResultToReturn = new PackageInspectionResult(
                    IsSuccess: true, FilePath: tempSource, Header: null,
                    Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
                )
            };

            var meshClassification = new MeshResourceClassification(
                entry.Id, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 GEOM", GameVersion.Sims3, Array.Empty<ConversionIssue>()
            );

            var meshRow = new MeshResourceRow(
                FormattedKey: entry.Id.FormattedKey, TypeHex: "0x015A1849", GroupHex: "0x00000000", InstanceHex: "0x0000000000000001",
                DataOffset: 128, CompressedSize: 100, DecompressedSize: 200, CompressionName: "Zlib",
                ClassificationKind: MeshClassificationKind.KnownMesh, RoleKind: MeshRoleKind.Geometry, FormatName: "TS3 GEOM",
                DetectedGameVersion: GameVersion.Sims3, CanExtractRawPayload: true, CanInspectCanonicalMesh: true,
                VertexCount: 100, FaceCount: 50, BoneCount: 2, HasNormals: true, HasUv0: true, HasBoneWeights: true, ValidationIssueCount: 0,
                Issues: Array.Empty<ConversionIssue>(), Entry: entry, Classification: meshClassification
            );

            var meshService = new FakeMeshInspectionService
            {
                ResultToReturn = new MeshInspectionResult(
                    IsSuccess: true, PackageFilePath: tempSource, Rows: new[] { meshRow }, Issues: Array.Empty<ConversionIssue>()
                )
            };

            var texService = new FakeTextureInspectionService
            {
                ResultToReturn = new TextureInspectionResult(
                    IsSuccess: true, PackageFilePath: tempSource, Rows: Array.Empty<TextureResourceRow>(), Issues: Array.Empty<ConversionIssue>()
                )
            };

            var service = new DecorativeObjectConversionService(pkgService, meshService, texService);
            var request = new DecorativeObjectConversionRequest(SourcePackagePath: tempSource, TargetOutputPath: tempTarget);

            var result = await service.CreateConversionPlanAsync(request);

            result.IsSuccess.Should().BeTrue();
            result.Plan.Should().NotBeNull();
            result.Plan!.SourcePackagePath.Should().Be(tempSource);
            result.Plan.TargetOutputPath.Should().Be(tempTarget);
            result.Plan.TotalMeshCandidateCount.Should().Be(1);
            result.Plan.ConvertableMeshCount.Should().Be(1);
            result.Plan.TotalTextureCandidateCount.Should().Be(0);
            result.Plan.IsFeasible.Should().BeTrue();

            result.Plan.Steps.Should().HaveCount(8);
            var capabilityStep = result.Plan.Steps.Single(s => s.StepId == "STEP-03B-CAPABILITY-MATRIX");
            capabilityStep.Status.Should().Be(DecorativeObjectConversionStepStatus.Completed);
            result.Plan.CapabilityMatrix.Should().NotBeNull();
            var graphStep = result.Plan.Steps.Single(s => s.StepId == "STEP-06-SOURCE-GRAPH");
            graphStep.Status.Should().Be(DecorativeObjectConversionStepStatus.Completed);
            graphStep.Details.Should().Contain("Built source asset graph");

            result.Plan.SourceGraph.Should().NotBeNull();
            result.Plan.SourceGraph!.IsSourceGraphReady.Should().BeTrue();
            result.Plan.SourceGraph.MeshAssets.Should().ContainSingle();

            var writerStep = result.Plan.Steps.Single(s => s.StepId == "STEP-07-TS4-WRITER");
            writerStep.Status.Should().Be(DecorativeObjectConversionStepStatus.NotImplemented);
            writerStep.Details.Should().Contain("NotImplemented");

            // Guarantee NO file creation on target output path during conversion plan!
            File.Exists(tempTarget).Should().BeFalse("CreateConversionPlan must NOT create target file on disk.");
        }
        finally
        {
            if (File.Exists(tempSource)) File.Delete(tempSource);
            if (File.Exists(tempTarget)) File.Delete(tempTarget);
        }
    }

    [Fact]
    public async Task CreateConversionPlanAsync_PackageWithoutTs3Geom_ReturnsPlanWithFailedSourceGraphStepAndIsFeasibleFalse()
    {
        var tempSource = Path.GetTempFileName();
        var tempTarget = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".package");

        try
        {
            var modlEntry = new PackageResourceEntry(new PackageResourceId(0x01661233, 0, 1), 100, 50, 100, PackageCompressionKind.Zlib, 0);
            var pkgRow = PackageResourceRow.FromEntry(modlEntry);

            var pkgService = new FakePackageInspectionService
            {
                ResultToReturn = new PackageInspectionResult(
                    IsSuccess: true, FilePath: tempSource, Header: null,
                    Resources: new[] { pkgRow }, Issues: Array.Empty<ConversionIssue>()
                )
            };

            var meshService = new FakeMeshInspectionService();
            var texService = new FakeTextureInspectionService();

            var service = new DecorativeObjectConversionService(pkgService, meshService, texService);
            var request = new DecorativeObjectConversionRequest(SourcePackagePath: tempSource, TargetOutputPath: tempTarget);

            var result = await service.CreateConversionPlanAsync(request);

            result.IsSuccess.Should().BeTrue();
            result.Plan.Should().NotBeNull();
            result.Plan!.IsFeasible.Should().BeFalse();

            var graphStep = result.Plan.Steps.Single(s => s.StepId == "STEP-06-SOURCE-GRAPH");
            graphStep.Status.Should().Be(DecorativeObjectConversionStepStatus.Failed);

            var writerStep = result.Plan.Steps.Single(s => s.StepId == "STEP-07-TS4-WRITER");
            writerStep.Status.Should().Be(DecorativeObjectConversionStepStatus.NotImplemented);

            File.Exists(tempTarget).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempSource)) File.Delete(tempSource);
            if (File.Exists(tempTarget)) File.Delete(tempTarget);
        }
    }
}
