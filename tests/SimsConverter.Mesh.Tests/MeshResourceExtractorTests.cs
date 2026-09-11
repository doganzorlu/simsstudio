using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Contracts;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Mesh.Tests;

public class MeshResourceExtractorTests
{
    private class FakePackageResourceExporter : IPackageResourceExporter
    {
        public PackageResourceExportRequest? CapturedRequest { get; private set; }
        public PackageResourceExportResult ReturnResult { get; set; } = new(
            IsSuccess: true,
            SourcePackagePath: "test.package",
            OutputFilePath: "target.ext",
            ExportedBytes: 500,
            Issues: Array.Empty<ConversionIssue>()
        );
        public bool WasCalled { get; private set; }

        public Task<PackageResourceExportResult> ExportAsync(
            PackageResourceExportRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            CapturedRequest = request;
            return Task.FromResult(ReturnResult with { OutputFilePath = request.OutputFilePath });
        }
    }

    private readonly FakePackageResourceExporter _fakePackageExporter = new();
    private readonly MeshResourceExtractor _extractor;

    public MeshResourceExtractorTests()
    {
        _extractor = new MeshResourceExtractor(_fakePackageExporter);
    }

    [Fact]
    public async Task ExtractAsync_KnownGeomMesh_DelegatesToPackageResourceExporterWithCorrectExtensionAndPath()
    {
        // Arrange
        var resId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x123456789ABCDEF0UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 Geometry (GEOM)", GameVersion.Sims3, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.WasCalled.Should().BeTrue();
        _fakePackageExporter.CapturedRequest.Should().NotBeNull();
        _fakePackageExporter.CapturedRequest!.SourcePackagePath.Should().Be("source.package");
        _fakePackageExporter.CapturedRequest.Offset.Should().Be(100);
        _fakePackageExporter.CapturedRequest.CompressedSize.Should().Be(500);
        _fakePackageExporter.CapturedRequest.OutputFilePath.Should().Be(Path.Combine("/output/dir", "015A1849_00000000_123456789ABCDEF0.geom"));
    }

    [Fact]
    public async Task ExtractAsync_SharedModelModl_UsesModlExtension()
    {
        // Arrange
        var resId = new PackageResourceId(MeshTypeIds.TsSharedModel, 0x00000000, 0x1111UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "Model (MODL)", GameVersion.Unknown, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.CapturedRequest!.OutputFilePath.Should().EndWith(".modl");
    }

    [Fact]
    public async Task ExtractAsync_SharedModelLodMlod_UsesMlodExtension()
    {
        // Arrange
        var resId = new PackageResourceId(MeshTypeIds.TsSharedModelLod, 0x00000000, 0x2222UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "Model LOD (MLOD)", GameVersion.Unknown, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.CapturedRequest!.OutputFilePath.Should().EndWith(".mlod");
    }

    [Fact]
    public async Task ExtractAsync_SharedRig_UsesRigExtension()
    {
        // Arrange
        var resId = new PackageResourceId(MeshTypeIds.TsSharedRig, 0x00000000, 0x3333UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Rig, "Rig / Skeleton", GameVersion.Unknown, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.CapturedRequest!.OutputFilePath.Should().EndWith(".rig");
    }

    [Fact]
    public async Task ExtractAsync_SharedSlot_UsesRsltExtension()
    {
        // Arrange
        var resId = new PackageResourceId(MeshTypeIds.TsSharedSlot, 0x00000000, 0x4444UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Slot, "Slot Layout (RSLT)", GameVersion.Unknown, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.CapturedRequest!.OutputFilePath.Should().EndWith(".rslt");
    }

    [Fact]
    public async Task ExtractAsync_SharedBlendGeometry_UsesBgeoExtension()
    {
        // Arrange
        var resId = new PackageResourceId(MeshTypeIds.TsSharedBlendGeometry, 0x00000000, 0x5555UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Morph, "Blend Geometry (BGEO)", GameVersion.Unknown, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.CapturedRequest!.OutputFilePath.Should().EndWith(".bgeo");
    }

    [Fact]
    public async Task ExtractAsync_ClassificationNotKnownMesh_FailsWithMESHE001AndDoesNotCallPackageExporter()
    {
        // Arrange: Unknown classification
        var resId = new PackageResourceId(0x77777777u, 0x00000000, 0x9999UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.Unknown, MeshRoleKind.Unknown, "Unknown Resource", GameVersion.Unknown, new[] { new ConversionIssue("MESHC001", "Warning", ConversionIssueSeverity.Warning) });
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _fakePackageExporter.WasCalled.Should().BeFalse("Extractor MUST NOT call Package Exporter when classification is not KnownMesh");
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("MESHE001");
    }

    [Fact]
    public async Task ExtractAsync_ResourceIdMismatch_FailsWithMESHE004AndDoesNotCallPackageExporter()
    {
        // Arrange: Mismatched ResourceIds
        var entryResId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x1111UL);
        var classResId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x2222UL);
        var entry = new PackageResourceEntry(entryResId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(classResId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 Geometry (GEOM)", GameVersion.Sims3, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _fakePackageExporter.WasCalled.Should().BeFalse("Extractor MUST NOT call Package Exporter when ResourceIds mismatch");
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("MESHE004");
    }

    [Fact]
    public async Task ExtractAsync_PathTraversalCustomFileName_SanitizesFileNameAndStaysInsideOutputDirectory()
    {
        // Arrange: CustomFileName with ../ path traversal
        string outputDir = Path.GetFullPath("/output/dir");
        var resId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x1234UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 Geometry (GEOM)", GameVersion.Sims3, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, outputDir, CustomFileName: "../../escape");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.CapturedRequest!.OutputFilePath.Should().StartWith(outputDir);
        _fakePackageExporter.CapturedRequest.OutputFilePath.Should().EndWith(".._.._escape.geom");
    }

    [Fact]
    public async Task ExtractAsync_AbsoluteCustomFileName_SanitizesFileNameAndStaysInsideOutputDirectory()
    {
        // Arrange: Absolute CustomFileName
        string outputDir = Path.GetFullPath("/output/dir");
        var resId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x1234UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 Geometry (GEOM)", GameVersion.Sims3, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, outputDir, CustomFileName: "/tmp/evil.geom");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.CapturedRequest!.OutputFilePath.Should().StartWith(outputDir);
        _fakePackageExporter.CapturedRequest.OutputFilePath.Should().EndWith("_tmp_evil.geom");
    }

    [Fact]
    public async Task ExtractAsync_TargetFilePathIdenticalToSourcePackagePath_FailsWithMESHE008()
    {
        // Arrange: Output path matches source package path
        var resId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x1234UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 Geometry (GEOM)", GameVersion.Sims3, Array.Empty<ConversionIssue>());
        string samePath = Path.GetFullPath("/output/dir/015A1849_00000000_0000000000001234.geom");
        var request = new MeshResourceExtractRequest(samePath, entry, classification, "/output/dir");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _fakePackageExporter.WasCalled.Should().BeFalse("Extractor MUST NOT call Package Exporter when target path is identical to source package");
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("MESHE008");
    }

    [Fact]
    public async Task ExtractAsync_NullOrEmptyInputs_ReturnsControlledFailureMESHE000()
    {
        // Arrange & Act
        var result1 = await _extractor.ExtractAsync(null!);
        var result2 = await _extractor.ExtractAsync(new MeshResourceExtractRequest("", null!, null!, ""));

        // Assert
        result1.IsSuccess.Should().BeFalse();
        result1.Issues[0].Code.Should().Be("MESHE000");

        result2.IsSuccess.Should().BeFalse();
        result2.Issues[0].Code.Should().Be("MESHE000");
    }

    [Fact]
    public async Task ExtractAsync_AllowOverwriteFlag_PreservedInPackageResourceExportRequest()
    {
        // Arrange
        var resId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x1234UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 Geometry (GEOM)", GameVersion.Sims3, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir", AllowOverwrite: true);

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.CapturedRequest!.AllowOverwrite.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_CustomFileName_UsesProvidedNameWithExtension()
    {
        // Arrange
        var resId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x1234UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 Geometry (GEOM)", GameVersion.Sims3, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir", CustomFileName: "my_mesh");

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fakePackageExporter.CapturedRequest!.OutputFilePath.Should().Be(Path.Combine("/output/dir", "my_mesh.geom"));
    }

    [Fact]
    public async Task ExtractAsync_PackageResourceExporterFailure_PreservesExporterIssuesInResult()
    {
        // Arrange
        var resId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x1234UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new MeshResourceClassification(resId, MeshClassificationKind.KnownMesh, MeshRoleKind.Geometry, "TS3 Geometry (GEOM)", GameVersion.Sims3, Array.Empty<ConversionIssue>());
        var request = new MeshResourceExtractRequest("source.package", entry, classification, "/output/dir");

        _fakePackageExporter.ReturnResult = new PackageResourceExportResult(
            IsSuccess: false,
            SourcePackagePath: "source.package",
            OutputFilePath: "/output/dir/015A1849_00000000_0000000000001234.geom",
            ExportedBytes: 0,
            Issues: new[] { new ConversionIssue("EXPE008", "Boundary failure", ConversionIssueSeverity.Error) }
        );

        // Act
        var result = await _extractor.ExtractAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("EXPE008");
    }
}
