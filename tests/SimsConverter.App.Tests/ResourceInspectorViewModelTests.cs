using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.App.Services;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Constants;
using SimsConverter.Textures.Contracts;
using SimsConverter.Textures.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.App.Tests;

public class ResourceInspectorViewModelTests
{
    private readonly ITextureResourceClassifier _textureClassifier = new TextureResourceClassifier();
    private readonly ITextureInspectionService _textureInspectionService;

    public ResourceInspectorViewModelTests()
    {
        _textureInspectionService = new TextureInspectionService(new FakeInspectionService(PackageInspectionResult.Failure("", "ERR", "Err")), _textureClassifier);
    }

    [Fact]
    public async Task BrowseAsync_GivenPickedFilePath_UpdatesSelectedFilePath()
    {
        // Arrange
        var fakePicker = new FakeFilePickerService("/picked/sample.package", "/dest/folder");
        var fakeInspectionService = new FakeInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var viewModel = new ResourceInspectorViewModel(fakeInspectionService, null, fakePicker);

        // Act
        await viewModel.BrowseCommand.ExecuteAsync(null);

        // Assert
        viewModel.SelectedFilePath.Should().Be("/picked/sample.package");
    }

    [Fact]
    public async Task BrowseAsync_GivenPickedSims3PackFilePath_UpdatesSelectedFilePath()
    {
        // Arrange
        var fakePicker = new FakeFilePickerService("/picked/sample.sims3pack", "/dest/folder");
        var fakeInspectionService = new FakeInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var viewModel = new ResourceInspectorViewModel(fakeInspectionService, null, fakePicker);

        // Act
        await viewModel.BrowseCommand.ExecuteAsync(null);

        // Assert
        viewModel.SelectedFilePath.Should().Be("/picked/sample.sims3pack");
    }

    [Fact]
    public async Task InspectAsync_GivenValidPackage_PopulatesResourcesAndTextureResources()
    {
        // Arrange
        var ddsRow = new PackageResourceRow(
            TextureTypeIds.Ts3DdsTexture,
            0x00000000u,
            0x123456789ABCDEF0UL,
            "0x00B2D882",
            "0x00000000",
            "0x123456789ABCDEF0",
            "00B2D882:00000000:123456789ABCDEF0",
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            "None"
        );

        var stubResult = new PackageInspectionResult(
            true,
            "/path/to/test.package",
            new DbpfHeader("DBPF", 2, 0, 1, 96, 32),
            new[] { ddsRow },
            Array.Empty<ConversionIssue>()
        );

        var stubService = new FakeInspectionService(stubResult);
        var viewModel = new ResourceInspectorViewModel(stubService, textureInspectionService: _textureInspectionService)
        {
            SelectedFilePath = "/path/to/test.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsBusy.Should().BeFalse();
        viewModel.HasIssues.Should().BeFalse();
        viewModel.IsSims3PackMode.Should().BeFalse();
        viewModel.Resources.Should().ContainSingle();
        viewModel.TextureResources.Should().ContainSingle();
        viewModel.HasTextureResources.Should().BeTrue();
        viewModel.StatusMessage.Should().Contain("1 resource entries (1 texture candidates, 0 mesh candidates).");
    }

    [Fact]
    public async Task InspectAsync_KnownDdsTextureSelected_SetsCanParseSelectedDdsHeaderAndCanExtractSelectedTextureTrue()
    {
        // Arrange: TS3 DDS Texture
        var ddsRow = new PackageResourceRow(
            TextureTypeIds.Ts3DdsTexture,
            0x00000000u,
            0x123456789ABCDEF0UL,
            "0x00B2D882",
            "0x00000000",
            "0x123456789ABCDEF0",
            "00B2D882:00000000:123456789ABCDEF0",
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            "None"
        );

        var stubResult = new PackageInspectionResult(
            true,
            "/path/to/test.package",
            new DbpfHeader("DBPF", 2, 0, 1, 96, 32),
            new[] { ddsRow },
            Array.Empty<ConversionIssue>()
        );

        var stubService = new FakeInspectionService(stubResult);
        var viewModel = new ResourceInspectorViewModel(stubService, textureInspectionService: _textureInspectionService)
        {
            SelectedFilePath = "/path/to/test.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);
        viewModel.SelectedTextureResource = viewModel.TextureResources[0];

        // Assert
        viewModel.CanExtractSelectedTexture.Should().BeTrue();
        viewModel.CanParseSelectedDdsHeader.Should().BeTrue();
    }

    [Fact]
    public async Task InspectAsync_Ts4Rle2TextureSelected_SetsCanParseSelectedDdsHeaderFalseAndCanExtractSelectedTextureTrue()
    {
        // Arrange: TS4 RLE2 Texture (0x3453CF95)
        var rle2Row = new PackageResourceRow(
            TextureTypeIds.Ts4Rle2Texture,
            0x00000000u,
            0x1111UL,
            "0x3453CF95",
            "0x00000000",
            "0x0000000000001111",
            "3453CF95:00000000:0000000000001111",
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            "None"
        );

        var stubResult = new PackageInspectionResult(
            true,
            "/path/to/test.package",
            new DbpfHeader("DBPF", 2, 0, 1, 96, 32),
            new[] { rle2Row },
            Array.Empty<ConversionIssue>()
        );

        var stubService = new FakeInspectionService(stubResult);
        var viewModel = new ResourceInspectorViewModel(stubService, textureInspectionService: _textureInspectionService)
        {
            SelectedFilePath = "/path/to/test.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);
        viewModel.SelectedTextureResource = viewModel.TextureResources[0];

        // Assert
        viewModel.CanExtractSelectedTexture.Should().BeTrue();
        viewModel.CanParseSelectedDdsHeader.Should().BeFalse();
    }

    [Fact]
    public async Task InspectAsync_UnknownResourceSelected_PreservedInTextureResourcesWithIssue()
    {
        // Arrange: Unknown TypeId 0x99999999
        var unknownRow = new PackageResourceRow(
            0x99999999u,
            0x00000000u,
            0x9999UL,
            "0x99999999",
            "0x00000000",
            "0x0000000000009999",
            "99999999:00000000:0000000000009999",
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            "None"
        );

        var stubResult = new PackageInspectionResult(
            true,
            "/path/to/test.package",
            new DbpfHeader("DBPF", 2, 0, 1, 96, 32),
            new[] { unknownRow },
            Array.Empty<ConversionIssue>()
        );

        var stubService = new FakeInspectionService(stubResult);
        var viewModel = new ResourceInspectorViewModel(stubService, textureInspectionService: _textureInspectionService)
        {
            SelectedFilePath = "/path/to/test.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);
        viewModel.SelectedTextureResource = viewModel.TextureResources[0];

        // Assert
        viewModel.TextureResources.Should().ContainSingle();
        viewModel.CanExtractSelectedTexture.Should().BeFalse();
        viewModel.CanParseSelectedDdsHeader.Should().BeFalse();
        viewModel.HasIssues.Should().BeTrue();
        viewModel.Issues.Should().Contain(i => i.Code == "TEXC001");
    }

    [Fact]
    public async Task InspectAsync_GivenInvalidPackage_ClearsTextureResources()
    {
        // Arrange
        var issue = new ConversionIssue("PARSE002", "Header magic invalid", ConversionIssueSeverity.Error);
        var stubResult = new PackageInspectionResult(
            false,
            "/path/to/corrupt.package",
            null,
            Array.Empty<PackageResourceRow>(),
            new[] { issue }
        );

        var stubService = new FakeInspectionService(stubResult);
        var viewModel = new ResourceInspectorViewModel(stubService, textureInspectionService: _textureInspectionService)
        {
            SelectedFilePath = "/path/to/corrupt.package"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsBusy.Should().BeFalse();
        viewModel.HasIssues.Should().BeTrue();
        viewModel.Resources.Should().BeEmpty();
        viewModel.TextureResources.Should().BeEmpty();
        viewModel.HasTextureResources.Should().BeFalse();
        viewModel.Issues.Should().ContainSingle();
        viewModel.Issues[0].Code.Should().Be("PARSE002");
    }

    [Fact]
    public async Task InspectAsync_GivenSims3PackFile_PopulatesSims3PackPayloadsAndMetadata()
    {
        // Arrange
        var stubPayloadRow = new Sims3PackPayloadRow(
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

        var stubResult = new Sims3PackInspectionResult(
            IsSuccess: true,
            FilePath: "/path/to/villa.sims3pack",
            RootElementName: "Sims3Pack",
            DeclaredEncoding: "utf-8",
            RawXmlSizeBytes: 150,
            Title: "Modern Villa",
            AssetId: "GUID-123",
            AssetType: "Lot",
            Description: "A villa",
            PayloadRows: new[] { stubPayloadRow },
            Issues: Array.Empty<ConversionIssue>()
        );

        var fakeDbpfService = new FakeInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var fakeS3PService = new FakeSims3PackInspectionService(stubResult);

        var viewModel = new ResourceInspectorViewModel(fakeDbpfService, sims3PackInspectionService: fakeS3PService)
        {
            SelectedFilePath = "/path/to/villa.sims3pack"
        };

        // Act
        await viewModel.InspectCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsBusy.Should().BeFalse();
        viewModel.IsSims3PackMode.Should().BeTrue();
        viewModel.Sims3PackTitle.Should().Be("Modern Villa");
        viewModel.Sims3PackPayloads.Should().ContainSingle();
        viewModel.Sims3PackPayloads[0].DisplayName.Should().Be("Villa Package");
        viewModel.StatusMessage.Should().Contain("1 embedded payload entries");
    }

    [Fact]
    public async Task ExportSims3PackPayloadAsync_GivenSelectedPayloadRow_DelegatesToSims3PackInspectionService()
    {
        // Arrange
        var stubPayloadRow = new Sims3PackPayloadRow(
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

        var stubInspectionResult = new Sims3PackInspectionResult(
            IsSuccess: true,
            FilePath: "/path/to/villa.sims3pack",
            RootElementName: "Sims3Pack",
            DeclaredEncoding: "utf-8",
            RawXmlSizeBytes: 150,
            Title: "Modern Villa",
            AssetId: "GUID-123",
            AssetType: "Lot",
            Description: "A villa",
            PayloadRows: new[] { stubPayloadRow },
            Issues: Array.Empty<ConversionIssue>()
        );

        var stubExportResult = new Sims3PackExportResult(
            IsSuccess: true,
            SourceSims3PackPath: "/path/to/villa.sims3pack",
            OutputFilePath: "/dest/folder/Villa Package.package",
            ExportedBytes: 1024,
            Issues: Array.Empty<ConversionIssue>()
        );

        var fakeDbpfService = new FakeInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var fakeS3PService = new FakeSims3PackInspectionService(stubInspectionResult, stubExportResult);
        var fakePicker = new FakeFilePickerService(null, "/dest/folder");

        var viewModel = new ResourceInspectorViewModel(fakeDbpfService, filePickerService: fakePicker, sims3PackInspectionService: fakeS3PService)
        {
            SelectedFilePath = "/path/to/villa.sims3pack",
            IsSims3PackMode = true,
            SelectedSims3PackPayload = stubPayloadRow
        };

        viewModel.CanExportSims3PackPayload.Should().BeTrue();

        // Act
        await viewModel.ExportSims3PackPayloadCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsBusy.Should().BeFalse();
        fakeS3PService.ExportCalled.Should().BeTrue();
        viewModel.StatusMessage.Should().Contain("exported successfully");
    }

    [Fact]
    public void FilePicker_ServiceSourceCodeMustSupportSims3PackPattern()
    {
        // Arrange
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string pickerServiceFile = Path.Combine(solutionDir, "src", "SimsConverter.App", "Services", "AvaloniaFilePickerService.cs");

        File.Exists(pickerServiceFile).Should().BeTrue("AvaloniaFilePickerService.cs must exist");

        string content = File.ReadAllText(pickerServiceFile);
        content.Should().Contain("*.sims3pack", "AvaloniaFilePickerService must include *.sims3pack in file picker patterns");
    }

    [Fact]
    public void CanInspect_ShouldBeFalse_WhenFilePathIsEmpty()
    {
        // Arrange
        var stubService = new FakeInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var viewModel = new ResourceInspectorViewModel(stubService)
        {
            SelectedFilePath = "   "
        };

        // Assert
        viewModel.CanInspect.Should().BeFalse();
        viewModel.InspectCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CanExport_ShouldBeFalse_WhenNoResourceIsSelected()
    {
        // Arrange
        var stubService = new FakeInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var viewModel = new ResourceInspectorViewModel(stubService)
        {
            SelectedFilePath = "/path/valid.package",
            SelectedResource = null
        };

        // Assert
        viewModel.CanExport.Should().BeFalse();
        viewModel.ExportResourceCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task ExportResourceAsync_DefaultAllowOverwriteIsFalse_AndPassesViewModelStateToRequest()
    {
        // Arrange
        var stubRow = new PackageResourceRow(
            0x00B2D882u,
            0x00000000u,
            0x123456789ABCDEF0UL,
            "0x00B2D882",
            "0x00000000",
            "0x123456789ABCDEF0",
            "00B2D882:00000000:123456789ABCDEF0",
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            "None"
        );

        var stubInspectionService = new FakeInspectionService(PackageInspectionResult.Failure("", "ERR", "Err"));
        var fakeExportResult = new PackageResourceExportResult(true, "/path/valid.package", "/dest/folder/00B2D882_00000000_123456789ABCDEF0.raw", 1024, Array.Empty<ConversionIssue>());
        var fakeExportService = new FakeExportService(fakeExportResult);
        var fakePicker = new FakeFilePickerService(null, "/dest/folder");

        var viewModel = new ResourceInspectorViewModel(stubInspectionService, fakeExportService, fakePicker)
        {
            SelectedFilePath = "/path/valid.package",
            SelectedResource = stubRow
        };

        viewModel.AllowOverwrite.Should().BeFalse("AllowOverwrite MUST default to false to prevent silent target file overwrite");
        viewModel.CanExport.Should().BeTrue();

        // Act
        await viewModel.ExportResourceCommand.ExecuteAsync(null);

        // Assert
        viewModel.IsBusy.Should().BeFalse();
        fakeExportService.LastRequest.Should().NotBeNull();
        fakeExportService.LastRequest!.DestinationDirectory.Should().Be("/dest/folder");
        fakeExportService.LastRequest.AllowOverwrite.Should().BeFalse();
        viewModel.StatusMessage.Should().Contain("exported successfully");
    }

    [Fact]
    public void SourceScan_AppSourceFilesShouldNotContainBinaryPrimitivesOrBinaryParsing()
    {
        // Arrange
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string appSourceDir = Path.Combine(solutionDir, "src", "SimsConverter.App");

        Directory.Exists(appSourceDir).Should().BeTrue("SimsConverter.App source directory must exist");

        string[] csFiles = Directory.GetFiles(appSourceDir, "*.cs", SearchOption.AllDirectories);
        csFiles.Should().NotBeEmpty();

        var forbiddenSymbols = new[] { "BinaryPrimitives", "System.Buffers.Binary", "FileStream", "ReadAllBytes" };

        foreach (var file in csFiles)
        {
            if (file.Contains(Path.Combine("obj", "Debug")) || file.Contains(Path.Combine("obj", "Release")))
            {
                continue;
            }

            string content = File.ReadAllText(file);
            foreach (var forbidden in forbiddenSymbols)
            {
                content.Should().NotContain(forbidden, $"File '{file}' in SimsConverter.App must not contain low-level binary symbol '{forbidden}'");
            }
        }
    }

    [Fact]
    public void XamlScan_AppViewsShouldComplyWithUIContract()
    {
        // Arrange
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string appSourceDir = Path.Combine(solutionDir, "src", "SimsConverter.App");

        string[] axamlFiles = Directory.GetFiles(appSourceDir, "*.axaml", SearchOption.AllDirectories);
        axamlFiles.Should().NotBeEmpty();

        var inlineHexRegex = new Regex(@"#(?:[0-9a-fA-F]{3,4}){1,2}\b", RegexOptions.Compiled);

        foreach (var file in axamlFiles)
        {
            if (file.Contains(Path.Combine("obj", "Debug")) || file.Contains(Path.Combine("obj", "Release")))
            {
                continue;
            }

            string content = File.ReadAllText(file);

            var hexMatches = inlineHexRegex.Matches(content);
            hexMatches.Should().BeEmpty($"XAML file '{file}' violates UI Contract by using inline hex color code(s): {string.Join(", ", hexMatches.Select(m => m.Value))}");

            if (Path.GetFileName(file) == "MainWindow.axaml")
            {
                content.Should().Contain("IsVisible=\"{Binding HasIssues}\"", "MainWindow.axaml must bind diagnostic issue visibility to HasIssues boolean property");
                content.Should().Contain("CellStyleClasses=\"monospaced\"", "MainWindow.axaml DataGrid hex key columns must specify monospaced cell style");
            }
        }
    }

    private sealed class FakeInspectionService : IPackageInspectionService
    {
        private readonly PackageInspectionResult _result;

        public FakeInspectionService(PackageInspectionResult result)
        {
            _result = result;
        }

        public Task<PackageInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeSims3PackInspectionService : ISims3PackInspectionService
    {
        private readonly Sims3PackInspectionResult _inspectionResult;
        private readonly Sims3PackExportResult _exportResult;
        public bool ExportCalled { get; private set; }

        public FakeSims3PackInspectionService(Sims3PackInspectionResult inspectionResult, Sims3PackExportResult? exportResult = null)
        {
            _inspectionResult = inspectionResult;
            _exportResult = exportResult ?? Sims3PackExportResult.Failure("", "", "ERR", "Error");
        }

        public Task<Sims3PackInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_inspectionResult);
        }

        public Task<Sims3PackExportResult> ExportPayloadAsync(Sims3PackExportRequest request, CancellationToken cancellationToken = default)
        {
            ExportCalled = true;
            return Task.FromResult(_exportResult);
        }
    }

    private sealed class FakeExportService : IResourceExportService
    {
        private readonly PackageResourceExportResult _result;
        public SingleResourceExportRequest? LastRequest { get; private set; }

        public FakeExportService(PackageResourceExportResult result)
        {
            _result = result;
        }

        public Task<PackageResourceExportResult> ExportResourceAsync(SingleResourceExportRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        private readonly string? _pickedPackagePath;
        private readonly string? _pickedFolderPath;

        public FakeFilePickerService(string? pickedPackagePath, string? pickedFolderPath = null)
        {
            _pickedPackagePath = pickedPackagePath;
            _pickedFolderPath = pickedFolderPath;
        }

        public Task<string?> OpenPackageFilePickerAsync()
        {
            return Task.FromResult(_pickedPackagePath);
        }

        public Task<string?> OpenFolderPickerAsync(string? title = null)
        {
            return Task.FromResult(_pickedFolderPath);
        }

        public Task<string?> SavePackageFilePickerAsync()
        {
            return Task.FromResult<string?>(null);
        }

        public Task CopyToClipboardAsync(string text)
        {
            return Task.CompletedTask;
        }

        public Task OpenFileWithDefaultAppAsync(string filePath)
        {
            return Task.CompletedTask;
        }
    }
}
