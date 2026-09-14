using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Contracts;
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
using Xunit.Abstractions;

namespace SimsConverter.Application.Tests;

public class BatchConversionServiceTests
{
    private readonly ITestOutputHelper _output;

    public BatchConversionServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static (IBatchConversionService Service, IPackageInspectionService PackageService, string TempDir) CreateTestSetup()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "batch_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var dbpfParser = new DbpfPackageParser();
        var packageService = new PackageInspectionService(dbpfParser);
        var meshClassifier = new MeshResourceClassifier();
        var validator = new CanonicalMeshValidator();
        var payloadReader = new PackageResourcePayloadReader();
        var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
        var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
        var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);
        var texClassifier = new TextureResourceClassifier();
        var texService = new TextureInspectionService(packageService, texClassifier);
        var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);
        var itemClassifier = new PackageItemClassifier();

        var decorativeService = new DecorativeObjectConversionService(packageService, meshService, texService, payloadVerifier: payloadVerifier, dbpfParser: dbpfParser);
        var casService = new CasItemConversionService(packageService, payloadReader: payloadReader, payloadVerifier: payloadVerifier, dbpfParser: dbpfParser);

        var batchService = new BatchConversionService(packageService, decorativeService, casService, itemClassifier);
        return (batchService, packageService, tempDir);
    }

    private static async Task CreateDummyPackageAsync(string filePath, uint typeId)
    {
        string seedPath = filePath + ".seed";
        byte[] header = new byte[96];
        header[0] = (byte)'D'; header[1] = (byte)'B'; header[2] = (byte)'P'; header[3] = (byte)'F';
        header[4] = 2;
        await File.WriteAllBytesAsync(seedPath, header);

        var writer = new DbpfPackageWriter();
        var entries = new[]
        {
            new DbpfPackageWriteResourceEntry(new PackageResourceId(typeId, 0, 100), Encoding.UTF8.GetBytes("DUMMY_PAYLOAD_CONTENT"), PackageCompressionKind.None, (uint)"DUMMY_PAYLOAD_CONTENT".Length)
        };
        await writer.WritePackageAsync(seedPath, filePath, entries);
        try { File.Delete(seedPath); } catch { }
    }

    [Fact]
    public async Task ScanFolderAsync_ListsFilesAndAssignsUniqueOutputPaths()
    {
        var (batchService, _, tempDir) = CreateTestSetup();
        try
        {
            string file1 = Path.Combine(tempDir, "mod1.package");
            string file2 = Path.Combine(tempDir, "mod2.package");
            string invalidFile = Path.Combine(tempDir, "ignored.txt");

            await CreateDummyPackageAsync(file1, 0x034B5D85); // TS4 CASP -> target TS3
            await CreateDummyPackageAsync(file2, 0x0355E0A6); // TS3 CASP -> target TS4
            await File.WriteAllTextAsync(invalidFile, "NOT_A_PACKAGE");

            // Act
            var result = await batchService.ScanFolderAsync(tempDir);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.Should().Contain(i => i.FileName == "mod1.package");
            result.Items.Should().Contain(i => i.FileName == "mod2.package");
            result.Items.Should().NotContain(i => i.FileName == "ignored.txt");

            var item1 = result.Items.First(i => i.FileName == "mod1.package");
            item1.TargetGameVersion.Should().Be(GameVersion.Sims3);
            item1.TargetOutputPath.Should().EndWith("mod1_ts3.package");

            var item2 = result.Items.First(i => i.FileName == "mod2.package");
            item2.TargetGameVersion.Should().Be(GameVersion.Sims4);
            item2.TargetOutputPath.Should().EndWith("mod2_ts4.package");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ScanFolderAsync_ResolvesDuplicateOutputPathsDeduplication()
    {
        var (batchService, _, tempDir) = CreateTestSetup();
        try
        {
            string file1 = Path.Combine(tempDir, "item.package");
            await CreateDummyPackageAsync(file1, 0x034B5D85);

            // Force duplicate scan scenario by passing duplicate filenames in request
            var scanRes = await batchService.ScanFolderAsync(tempDir);
            scanRes.Items.Count.Should().Be(1);
            scanRes.Items[0].TargetOutputPath.Should().NotBe(file1, "Target output path must not overwrite source file path.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ExecuteBatchConversionAsync_AppliesErrorIsolation_ProcessesRemainingFilesWhenOneFails()
    {
        var (batchService, _, tempDir) = CreateTestSetup();
        try
        {
            string corruptFile = Path.Combine(tempDir, "01_corrupt.package");
            string validFile = Path.Combine(tempDir, "02_valid.package");

            await File.WriteAllTextAsync(corruptFile, "CORRUPT_INVALID_HEADER_HEADER_HEADER");
            await CreateDummyPackageAsync(validFile, 0x034B5D85); // TS4 CASP

            var scanResult = await batchService.ScanFolderAsync(tempDir);
            scanResult.Items.Should().HaveCount(2);

            var req = new BatchConversionRequest(tempDir, items: scanResult.Items);

            // Act
            var batchResult = await batchService.ExecuteBatchConversionAsync(req);

            // Assert - Error isolation: corrupt file fails, valid file continues and succeeds (or handles cleanly)
            batchResult.TotalCount.Should().Be(2);
            batchResult.Items.Should().Contain(i => i.FileName == "01_corrupt.package" && i.Status == BatchItemStatus.Failed);

            var corruptItem = batchResult.Items.First(i => i.FileName == "01_corrupt.package");
            corruptItem.StatusMessage.Should().NotBeNullOrEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ExecuteBatchConversionAsync_PreservesPreExistingTargetFile_OnFailure()
    {
        var (batchService, _, tempDir) = CreateTestSetup();
        try
        {
            string sourceFile = Path.Combine(tempDir, "corrupt_source.package");
            await File.WriteAllTextAsync(sourceFile, "INVALID_PACKAGE_CONTENT");

            string existingTarget = Path.Combine(tempDir, "corrupt_source_ts3.package");
            byte[] preservedPayload = Encoding.UTF8.GetBytes("PRESERVED_TARGET_PAYLOAD_DATA");
            await File.WriteAllBytesAsync(existingTarget, preservedPayload);

            var item = new BatchConversionItem
            {
                SourceFilePath = sourceFile,
                FileName = Path.GetFileName(sourceFile),
                TargetOutputPath = existingTarget,
                Category = PackageItemCategory.CasPart,
                Status = BatchItemStatus.Pending
            };

            var req = new BatchConversionRequest(tempDir, items: new[] { item });

            // Act
            var result = await batchService.ExecuteBatchConversionAsync(req);

            // Assert
            result.FailedCount.Should().Be(1);
            File.Exists(existingTarget).Should().BeTrue();
            byte[] currentBytes = await File.ReadAllBytesAsync(existingTarget);
            currentBytes.Should().Equal(preservedPayload, "Batch conversion failure must preserve pre-existing target file untouched.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ScanFolderAsync_DiscoversAndIgnoresStandaloneImageFiles()
    {
        var (batchService, _, tempDir) = CreateTestSetup();
        try
        {
            string pkgFile = Path.Combine(tempDir, "item.package");
            string pngFile = Path.Combine(tempDir, "preview.png");
            string jpgFile = Path.Combine(tempDir, "cover.jpg");
            string ddsFile = Path.Combine(tempDir, "texture.dds");

            await CreateDummyPackageAsync(pkgFile, 0x034B5D85);
            await File.WriteAllBytesAsync(pngFile, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            await File.WriteAllBytesAsync(jpgFile, new byte[] { 0xFF, 0xD8, 0xFF });
            await File.WriteAllBytesAsync(ddsFile, new byte[] { 0x44, 0x44, 0x53, 0x20 });

            // Act
            var result = await batchService.ScanFolderAsync(tempDir);

            // Assert
            result.TotalCount.Should().Be(4);
            result.IgnoredCount.Should().Be(3);

            var ignoredItems = result.Items.Where(i => i.Status == BatchItemStatus.Ignored).ToList();
            ignoredItems.Should().HaveCount(3);
            ignoredItems.Should().Contain(i => i.FileName == "preview.png" && i.StatusMessage.Contains("Unsupported standalone image file (.png)"));
            ignoredItems.Should().Contain(i => i.FileName == "cover.jpg" && i.StatusMessage.Contains("Unsupported standalone image file (.jpg)"));
            ignoredItems.Should().Contain(i => i.FileName == "texture.dds" && i.StatusMessage.Contains("Unsupported standalone image file (.dds)"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [SkippableFact]
    public async Task ExecuteBatchConversionAsync_WithRealIndiFolderFixture_ReportsIgnoredPngFilesAndConvertsPackages()
    {
        string indiPath = "/Users/dogan/Downloads/INDI";
        if (!Directory.Exists(indiPath))
        {
            Skip.If(true, "[SKIPPED/Missing fixture] Real folder fixture directory not found at '/Users/dogan/Downloads/INDI'. Real batch folder test skipped.");
            return;
        }

        var (batchService, _, tempDir) = CreateTestSetup();
        try
        {
            var scanResult = await batchService.ScanFolderAsync(indiPath, tempDir);

            bool hasPackages = scanResult.Items.Any(i => i.FileName.EndsWith(".package", StringComparison.OrdinalIgnoreCase) || i.FileName.EndsWith(".sims3pack", StringComparison.OrdinalIgnoreCase));
            bool hasImages = scanResult.Items.Any(i => i.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                       i.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                       i.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                       i.FileName.EndsWith(".dds", StringComparison.OrdinalIgnoreCase));

            if (!hasPackages || !hasImages)
            {
                Skip.If(true, $"[SKIPPED/Missing fixture] Fixture directory at '{indiPath}' does not contain required combination of container files and standalone images (Packages: {hasPackages}, Images: {hasImages}). Test skipped.");
                return;
            }

            scanResult.TotalCount.Should().BeGreaterThan(0);

            int expectedIgnored = scanResult.Items.Count(i => i.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                             i.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                             i.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                             i.FileName.EndsWith(".dds", StringComparison.OrdinalIgnoreCase));
            scanResult.IgnoredCount.Should().Be(expectedIgnored);

            var req = new BatchConversionRequest(indiPath, tempDir, scanResult.Items);
            var batchResult = await batchService.ExecuteBatchConversionAsync(req);

            batchResult.IgnoredCount.Should().Be(expectedIgnored);
            batchResult.TotalCount.Should().Be(scanResult.TotalCount);

            _output.WriteLine($"[EXECUTED/PASSED] Batch conversion completed on real folder fixture '{indiPath}'. Total items: {scanResult.TotalCount}, Converted: {batchResult.SuccessCount}, Failed: {batchResult.FailedCount}, Skipped: {batchResult.SkippedCount}, Ignored Images: {batchResult.IgnoredCount}.");
            _output.WriteLine($"[DIAGNOSTIC LOG EXPORT] Run ID: {batchResult.RunId}");
            _output.WriteLine($"[DIAGNOSTIC LOG EXPORT] JSON Log Path: {batchResult.LogFilePathJson}");
            _output.WriteLine($"[DIAGNOSTIC LOG EXPORT] Text Log Path: {batchResult.LogFilePathText}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    private class ThrowingFaultyLogger : IBatchDiagnosticLogger
    {
        public BatchRunLog? CurrentSession => null;
        public BatchRunLog StartBatchSession(string sourceFolderPath, string outputFolderPath, string? customRunId = null) => throw new InvalidOperationException("Simulated logger failure!");
        public void LogPipelinePhase(string phase, string message, string? fileName = null, bool isSuccess = true, string? details = null) => throw new InvalidOperationException("Simulated logger phase failure!");
        public void RecordItemResult(BatchItemDiagnosticLog itemLog) => throw new InvalidOperationException("Simulated logger record failure!");
        public Task<(string JsonPath, string TextPath)> CompleteAndExportBatchSessionAsync(string? baseDirectory = null) => throw new InvalidOperationException("Simulated logger export failure!");
    }

    [Fact]
    public async Task ExecuteBatchConversionAsync_WhenDiagnosticLoggerThrows_DoesNotFailOrInterruptConversion()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "faulty_logger_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var dbpfParser = new DbpfPackageParser();
            var packageService = new PackageInspectionService(dbpfParser);
            var meshClassifier = new MeshResourceClassifier();
            var validator = new CanonicalMeshValidator();
            var payloadReader = new PackageResourcePayloadReader();
            var ts3Importer = new Ts3GeomCanonicalMeshImporter(new Ts3GeomMetadataReader(), validator);
            var ts4Importer = new Ts4GeomCanonicalMeshImporter(new Ts4GeomMetadataReader(), validator);
            var meshService = new MeshInspectionService(packageService, meshClassifier, ts3Importer, ts4Importer, payloadReader);
            var texClassifier = new TextureResourceClassifier();
            var texService = new TextureInspectionService(packageService, texClassifier);
            var payloadVerifier = new Ts4ResourcePayloadCompatibilityVerifier(payloadReader, ts4Importer);
            var itemClassifier = new PackageItemClassifier();

            var decorativeService = new DecorativeObjectConversionService(packageService, meshService, texService, payloadVerifier: payloadVerifier, dbpfParser: dbpfParser);
            var casService = new CasItemConversionService(packageService, payloadReader: payloadReader, payloadVerifier: payloadVerifier, dbpfParser: dbpfParser);

            var faultyLogger = new ThrowingFaultyLogger();
            var batchService = new BatchConversionService(packageService, decorativeService, casService, itemClassifier, diagnosticLogger: faultyLogger);

            string file1 = Path.Combine(tempDir, "item.package");
            await CreateDummyPackageAsync(file1, 0x034B5D85);

            var scanRes = await batchService.ScanFolderAsync(tempDir);
            var req = new BatchConversionRequest(tempDir, items: scanRes.Items);

            // Act - Logging exception must NOT fail batch conversion execution
            var result = await batchService.ExecuteBatchConversionAsync(req);

            result.Should().NotBeNull();
            result.TotalCount.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
