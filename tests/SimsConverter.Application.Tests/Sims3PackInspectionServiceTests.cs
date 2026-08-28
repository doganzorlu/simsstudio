using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Application.Tests;

public class Sims3PackInspectionServiceTests
{
    private readonly StubSims3PackDetector _detector = new();
    private readonly StubSims3PackXmlParser _xmlParser = new();
    private readonly StubSims3PackPayloadCatalogScanner _catalogScanner = new();
    private readonly StubSims3PackPayloadExporter _exporter = new();
    private readonly Sims3PackInspectionService _service;

    public Sims3PackInspectionServiceTests()
    {
        _service = new Sims3PackInspectionService(_detector, _xmlParser, _catalogScanner, _exporter);
    }

    [Fact]
    public async Task InspectFileAsync_GivenValidSims3Pack_ReturnsMetadataAndUiReadyPayloadRows()
    {
        // Arrange
        string testPath = Path.Combine(Path.GetTempPath(), "valid_" + Guid.NewGuid() + ".sims3pack");
        await File.WriteAllBytesAsync(testPath, new byte[100]);

        _detector.ResultToReturn = new PackageDetectionResult(
            ContainerKind: PackageContainerKind.Sims3Pack,
            DetectedGameVersion: GameVersion.Sims3,
            Confidence: PackageDetectionConfidence.High,
            MajorVersion: "1",
            MinorVersion: "0",
            IndexEntryCount: 0,
            Issues: Array.Empty<ConversionIssue>()
        );

        _xmlParser.ResultToReturn = new Sims3PackParseResult(
            IsSuccess: true,
            Metadata: new Sims3PackXmlMetadata(
                RootElementName: "Sims3Pack",
                DeclaredEncoding: "utf-8",
                RawXmlSizeBytes: 120,
                EmbeddedFileCount: 1,
                EmbeddedFileNames: new[] { "lot.package" },
                Title: "Modern Villa",
                AssetId: "GUID-123",
                AssetType: "Lot",
                Description: "A villa"
            ),
            Issues: Array.Empty<ConversionIssue>()
        );

        _catalogScanner.ResultToReturn = new Sims3PackCatalogResult(
            IsSuccess: true,
            Entries: new[]
            {
                new Sims3PackCatalogEntry(1, Sims3PackPayloadKind.DbpfPackage, 500, 1024, "Villa Package", Array.Empty<ConversionIssue>()),
                new Sims3PackCatalogEntry(2, Sims3PackPayloadKind.PngPreview, 2000, 512, "Preview Image", Array.Empty<ConversionIssue>())
            },
            Issues: Array.Empty<ConversionIssue>()
        );

        try
        {
            // Act
            var result = await _service.InspectFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Title.Should().Be("Modern Villa");
            result.PayloadRows.Should().HaveCount(2);

            result.PayloadRows[0].Kind.Should().Be("DbpfPackage");
            result.PayloadRows[0].DataOffsetHex.Should().Be("0x000001F4");
            result.PayloadRows[0].CanExport.Should().BeTrue();

            result.PayloadRows[1].Kind.Should().Be("PngPreview");
            result.PayloadRows[1].CanExport.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task InspectFileAsync_GivenXmlParseFailure_ReturnsControlledFailureAndDoesNotCallCatalogScanner()
    {
        // Arrange
        string testPath = Path.Combine(Path.GetTempPath(), "xml_fail_" + Guid.NewGuid() + ".sims3pack");
        await File.WriteAllBytesAsync(testPath, new byte[100]);

        _detector.ResultToReturn = new PackageDetectionResult(
            ContainerKind: PackageContainerKind.Sims3Pack,
            DetectedGameVersion: GameVersion.Sims3,
            Confidence: PackageDetectionConfidence.High,
            MajorVersion: "1",
            MinorVersion: "0",
            IndexEntryCount: 0,
            Issues: Array.Empty<ConversionIssue>()
        );

        _xmlParser.ResultToReturn = Sims3PackParseResult.Failure("S3PX001", "Malformed XML metadata.");

        try
        {
            // Act
            var result = await _service.InspectFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PX001");
            _catalogScanner.ScanFileCalled.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task InspectFileAsync_GivenCatalogScannerFailure_ReturnsControlledFailureWithAggregatedIssues()
    {
        // Arrange
        string testPath = Path.Combine(Path.GetTempPath(), "catalog_fail_" + Guid.NewGuid() + ".sims3pack");
        await File.WriteAllBytesAsync(testPath, new byte[100]);

        _detector.ResultToReturn = new PackageDetectionResult(
            ContainerKind: PackageContainerKind.Sims3Pack,
            DetectedGameVersion: GameVersion.Sims3,
            Confidence: PackageDetectionConfidence.High,
            MajorVersion: "1",
            MinorVersion: "0",
            IndexEntryCount: 0,
            Issues: Array.Empty<ConversionIssue>()
        );

        _xmlParser.ResultToReturn = new Sims3PackParseResult(
            IsSuccess: true,
            Metadata: new Sims3PackXmlMetadata("Sims3Pack", "utf-8", 100, 0, Array.Empty<string>(), null, null, null, null),
            Issues: Array.Empty<ConversionIssue>()
        );

        _catalogScanner.ResultToReturn = Sims3PackCatalogResult.Failure("S3PC003", "Premature EOS during catalog scan.");

        try
        {
            // Act
            var result = await _service.InspectFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PC003");
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task InspectFileAsync_GivenEmptyCatalog_ReturnsSuccessWithEmptyPayloadRows()
    {
        // Arrange
        string testPath = Path.Combine(Path.GetTempPath(), "empty_cat_" + Guid.NewGuid() + ".sims3pack");
        await File.WriteAllBytesAsync(testPath, new byte[100]);

        _detector.ResultToReturn = new PackageDetectionResult(
            ContainerKind: PackageContainerKind.Sims3Pack,
            DetectedGameVersion: GameVersion.Sims3,
            Confidence: PackageDetectionConfidence.High,
            MajorVersion: "1",
            MinorVersion: "0",
            IndexEntryCount: 0,
            Issues: Array.Empty<ConversionIssue>()
        );

        _xmlParser.ResultToReturn = new Sims3PackParseResult(
            IsSuccess: true,
            Metadata: new Sims3PackXmlMetadata("Sims3Pack", "utf-8", 100, 0, Array.Empty<string>(), null, null, null, null),
            Issues: Array.Empty<ConversionIssue>()
        );

        _catalogScanner.ResultToReturn = new Sims3PackCatalogResult(true, Array.Empty<Sims3PackCatalogEntry>(), Array.Empty<ConversionIssue>());

        try
        {
            // Act
            var result = await _service.InspectFileAsync(testPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.PayloadRows.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    [Fact]
    public async Task ExportPayloadAsync_GivenValidDbpfRow_DelegatesToExporterAndReturnsSuccess()
    {
        // Arrange
        string sourcePath = Path.Combine(Path.GetTempPath(), "export_source_" + Guid.NewGuid() + ".sims3pack");
        string outputDir = Path.GetTempPath();

        var dbpfRow = new Sims3PackPayloadRow(
            EntryIndex: 1,
            Kind: "DbpfPackage",
            DataOffset: 500,
            DataOffsetHex: "0x000001F4",
            EstimatedSizeBytes: 1024,
            EstimatedSizeFormatted: "1.0 KB",
            DisplayName: "Villa Package",
            CanExport: true,
            Issues: Array.Empty<ConversionIssue>()
        );

        _exporter.ResultToReturn = new Sims3PackPayloadExportResult(
            IsSuccess: true,
            SourceSims3PackPath: sourcePath,
            OutputFilePath: Path.Combine(outputDir, "Villa Package.package"),
            ExportedBytes: 1024,
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackExportRequest(sourcePath, dbpfRow, outputDir);

        // Act
        var result = await _service.ExportPayloadAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ExportedBytes.Should().Be(1024);
        _exporter.ExportCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExportPayloadAsync_GivenNonDbpfRow_ReturnsControlledFailureWithoutDelegating()
    {
        // Arrange
        string sourcePath = Path.Combine(Path.GetTempPath(), "export_png_" + Guid.NewGuid() + ".sims3pack");
        string outputDir = Path.GetTempPath();

        var pngRow = new Sims3PackPayloadRow(
            EntryIndex: 2,
            Kind: "PngPreview",
            DataOffset: 2000,
            DataOffsetHex: "0x000007D0",
            EstimatedSizeBytes: 512,
            EstimatedSizeFormatted: "512 B",
            DisplayName: "Preview Image",
            CanExport: false,
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackExportRequest(sourcePath, pngRow, outputDir);

        // Act
        var result = await _service.ExportPayloadAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("S3PA005");
        _exporter.ExportCalled.Should().BeFalse();
    }

    private sealed class StubSims3PackDetector : ISims3PackDetector
    {
        public PackageDetectionResult ResultToReturn { get; set; } = PackageDetectionResult.Unknown("ERR", "Error");
        public Task<PackageDetectionResult> DetectFileAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(ResultToReturn);
        public Task<PackageDetectionResult> DetectAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) => Task.FromResult(ResultToReturn);
    }

    private sealed class StubSims3PackXmlParser : ISims3PackXmlParser
    {
        public Sims3PackParseResult ResultToReturn { get; set; } = Sims3PackParseResult.Failure("ERR", "Error");
        public Task<Sims3PackParseResult> ParseFileAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(ResultToReturn);
        public Task<Sims3PackParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default) => Task.FromResult(ResultToReturn);
    }

    private sealed class StubSims3PackPayloadCatalogScanner : ISims3PackPayloadCatalogScanner
    {
        public bool ScanFileCalled { get; private set; }
        public Sims3PackCatalogResult ResultToReturn { get; set; } = Sims3PackCatalogResult.Failure("ERR", "Error");
        public Task<Sims3PackCatalogResult> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ScanFileCalled = true;
            return Task.FromResult(ResultToReturn);
        }
        public Task<Sims3PackCatalogResult> ScanAsync(Stream stream, CancellationToken cancellationToken = default) => Task.FromResult(ResultToReturn);
    }

    private sealed class StubSims3PackPayloadExporter : ISims3PackPayloadExporter
    {
        public bool ExportCalled { get; private set; }
        public Sims3PackPayloadExportResult ResultToReturn { get; set; } = Sims3PackPayloadExportResult.Failure("", "", "ERR", "Error");
        public Task<Sims3PackPayloadExportResult> ExportAsync(Sims3PackPayloadExportRequest request, CancellationToken cancellationToken = default)
        {
            ExportCalled = true;
            return Task.FromResult(ResultToReturn);
        }
    }
}
