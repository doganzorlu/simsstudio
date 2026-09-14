using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.App.Services;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Domain.Services;
using SimsConverter.Mesh.Services;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Services;
using Xunit;

namespace SimsConverter.App.Tests;

public class ResourceInspectorViewModelBatchTests
{
    private static (ResourceInspectorViewModel ViewModel, string TempDir) CreateTestViewModel(IFilePickerService? filePickerService = null)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "vm_batch_test_" + Guid.NewGuid().ToString("N"));
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

        var viewModel = new ResourceInspectorViewModel(
            inspectionService: packageService,
            textureInspectionService: texService,
            meshInspectionService: meshService,
            conversionService: decorativeService,
            casConversionService: casService,
            batchConversionService: batchService,
            filePickerService: filePickerService
        );

        return (viewModel, tempDir);
    }

    private sealed class TestFilePickerService : IFilePickerService
    {
        private readonly string? _folderPath;
        public string? LastRequestedTitle { get; private set; }

        public TestFilePickerService(string? folderPath)
        {
            _folderPath = folderPath;
        }

        public Task<string?> OpenPackageFilePickerAsync() => Task.FromResult<string?>(null);

        public Task<string?> OpenFolderPickerAsync(string? title = null)
        {
            LastRequestedTitle = title;
            return Task.FromResult(_folderPath);
        }

        public Task<string?> SavePackageFilePickerAsync() => Task.FromResult<string?>(null);

        public Task CopyToClipboardAsync(string text)
        {
            LastCopiedText = text;
            return Task.CompletedTask;
        }

        public Task OpenFileWithDefaultAppAsync(string filePath)
        {
            LastOpenedFilePath = filePath;
            return Task.CompletedTask;
        }

        public string? LastCopiedText { get; private set; }
        public string? LastOpenedFilePath { get; private set; }
    }

    [Fact]
    public async Task BrowseFolderCommand_WithValidPath_SetsSourceAndDefaultTarget_AndPassesTitle()
    {
        var picker = new TestFilePickerService("/path/to/source");
        var (vm, tempDir) = CreateTestViewModel(picker);
        try
        {
            await vm.BrowseFolderCommand.ExecuteAsync(null);

            vm.BatchSourceFolderPath.Should().Be("/path/to/source");
            vm.BatchOutputFolderPath.Should().Be("/path/to/source");
            picker.LastRequestedTitle.Should().Be("Select Source Folder");
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task BrowseFolderCommand_WhenCancelled_PreservesExistingFolderPaths()
    {
        var picker = new TestFilePickerService(null);
        var (vm, tempDir) = CreateTestViewModel(picker);
        try
        {
            vm.BatchSourceFolderPath = "/existing/source";
            vm.BatchOutputFolderPath = "/existing/target";

            await vm.BrowseFolderCommand.ExecuteAsync(null);

            vm.BatchSourceFolderPath.Should().Be("/existing/source");
            vm.BatchOutputFolderPath.Should().Be("/existing/target");
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task BrowseBatchOutputFolderCommand_WithValidPathAndCancel_BehavesCorrectly()
    {
        var picker = new TestFilePickerService("/path/to/target");
        var (vm, tempDir) = CreateTestViewModel(picker);
        try
        {
            await vm.BrowseBatchOutputFolderCommand.ExecuteAsync(null);
            vm.BatchOutputFolderPath.Should().Be("/path/to/target");
            picker.LastRequestedTitle.Should().Be("Select Target Output Folder");

            var cancelPicker = new TestFilePickerService(null);
            var (cancelVm, tempDir2) = CreateTestViewModel(cancelPicker);
            cancelVm.BatchOutputFolderPath = "/initial/target";
            await cancelVm.BrowseBatchOutputFolderCommand.ExecuteAsync(null);
            cancelVm.BatchOutputFolderPath.Should().Be("/initial/target");
            if (Directory.Exists(tempDir2)) try { Directory.Delete(tempDir2, true); } catch { }
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ScanBatchFolderCommand_PopulatesBatchItemsAndCounters()
    {
        var (vm, tempDir) = CreateTestViewModel();
        try
        {
            string file1 = Path.Combine(tempDir, "item1.package");
            string file2 = Path.Combine(tempDir, "item2.package");
            string seed1 = file1 + ".seed";
            string seed2 = file2 + ".seed";

            byte[] header = new byte[96];
            header[0] = (byte)'D'; header[1] = (byte)'B'; header[2] = (byte)'P'; header[3] = (byte)'F';
            header[4] = 2;
            await File.WriteAllBytesAsync(seed1, header);
            await File.WriteAllBytesAsync(seed2, header);

            var writer = new DbpfPackageWriter();
            var entries = new[]
            {
                new DbpfPackageWriteResourceEntry(new PackageResourceId(0x034B5D85, 0, 1), Encoding.UTF8.GetBytes("PAYLOAD"), PackageCompressionKind.None, 7)
            };
            await writer.WritePackageAsync(seed1, file1, entries);
            await writer.WritePackageAsync(seed2, file2, entries);
            try { File.Delete(seed1); File.Delete(seed2); } catch { }

            vm.BatchSourceFolderPath = tempDir;

            // Act
            await vm.ScanBatchFolderCommand.ExecuteAsync(null);

            // Assert
            vm.HasBatchItems.Should().BeTrue();
            vm.BatchTotalCount.Should().Be(2);
            vm.BatchPendingCount.Should().Be(2);
            vm.BatchItems.Should().HaveCount(2);
            vm.StatusMessage.Should().Contain("Discovered 2 total candidate container file(s)");
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
    public async Task ConvertBatchCommand_UpdatesItemsAndSummaryCounters()
    {
        var (vm, tempDir) = CreateTestViewModel();
        try
        {
            string file1 = Path.Combine(tempDir, "valid_item.package");
            string seed1 = file1 + ".seed";
            byte[] header = new byte[96];
            header[0] = (byte)'D'; header[1] = (byte)'B'; header[2] = (byte)'P'; header[3] = (byte)'F';
            header[4] = 2;
            await File.WriteAllBytesAsync(seed1, header);

            var writer = new DbpfPackageWriter();
            var entries = new[]
            {
                new DbpfPackageWriteResourceEntry(new PackageResourceId(0x034B5D85, 0, 1), Encoding.UTF8.GetBytes("CASP_PAYLOAD_TEST"), PackageCompressionKind.None, 17)
            };
            await writer.WritePackageAsync(seed1, file1, entries);
            try { File.Delete(seed1); } catch { }

            vm.BatchSourceFolderPath = tempDir;
            await vm.ScanBatchFolderCommand.ExecuteAsync(null);

            // Act
            await vm.ConvertBatchCommand.ExecuteAsync(null);

            // Assert
            vm.BatchTotalCount.Should().Be(1);
            vm.StatusMessage.Should().Contain("Batch conversion finished!");
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
    public async Task ScanBatchFolderCommand_UpdatesBatchIgnoredCount_WhenImagesPresent()
    {
        var (vm, tempDir) = CreateTestViewModel();
        try
        {
            string file1 = Path.Combine(tempDir, "item1.package");
            string image1 = Path.Combine(tempDir, "preview.png");
            string seed1 = file1 + ".seed";

            byte[] header = new byte[96];
            header[0] = (byte)'D'; header[1] = (byte)'B'; header[2] = (byte)'P'; header[3] = (byte)'F';
            header[4] = 2;
            await File.WriteAllBytesAsync(seed1, header);

            var writer = new DbpfPackageWriter();
            var entries = new[]
            {
                new DbpfPackageWriteResourceEntry(new PackageResourceId(0x034B5D85, 0, 1), Encoding.UTF8.GetBytes("PAYLOAD"), PackageCompressionKind.None, 7)
            };
            await writer.WritePackageAsync(seed1, file1, entries);
            try { File.Delete(seed1); } catch { }

            await File.WriteAllBytesAsync(image1, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            vm.BatchSourceFolderPath = tempDir;

            // Act
            await vm.ScanBatchFolderCommand.ExecuteAsync(null);

            // Assert
            vm.HasBatchItems.Should().BeTrue();
            vm.BatchTotalCount.Should().Be(2);
            vm.BatchPendingCount.Should().Be(1);
            vm.BatchIgnoredCount.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
