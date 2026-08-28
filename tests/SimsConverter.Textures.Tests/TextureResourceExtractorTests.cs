using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using SimsConverter.Textures.Constants;
using SimsConverter.Textures.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Textures.Tests;

public class TextureResourceExtractorTests
{
    private readonly StubPackageResourceExporter _packageExporter = new();
    private readonly TextureResourceExtractor _extractor;

    public TextureResourceExtractorTests()
    {
        _extractor = new TextureResourceExtractor(_packageExporter);
    }

    [Fact]
    public async Task ExportAsync_KnownDdsTexture_DelegatesToPackageExporterWithCorrectOffsetAndSize()
    {
        // Arrange
        string sourcePath = Path.Combine(Path.GetTempPath(), "test_source_" + Guid.NewGuid() + ".package");
        string outputDir = Path.GetTempPath();
        await File.WriteAllBytesAsync(sourcePath, new byte[100]);

        var resId = new PackageResourceId(TextureTypeIds.Ts3DdsTexture, 0, 0x123456789ABCDEF0UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new TextureResourceClassification(resId, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS Image Texture", GameVersion.Sims3, Array.Empty<ConversionIssue>());

        _packageExporter.ResultToReturn = new PackageResourceExportResult(
            IsSuccess: true,
            SourcePackagePath: sourcePath,
            OutputFilePath: Path.Combine(outputDir, "00B2D882_00000000_123456789ABCDEF0.dds"),
            ExportedBytes: 500,
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new TextureResourceExtractRequest(sourcePath, entry, classification, outputDir);

        try
        {
            // Act
            var result = await _extractor.ExtractAsync(request);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.OutputFilePath.Should().EndWith(".dds");
            _packageExporter.ExportCalled.Should().BeTrue();
            _packageExporter.LastRequest.Should().NotBeNull();
            _packageExporter.LastRequest!.Offset.Should().Be(100);
            _packageExporter.LastRequest.CompressedSize.Should().Be(500);
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task ExportAsync_MismatchedClassificationAndEntry_ReturnsControlledFailureWithoutDelegating()
    {
        // Arrange
        string sourcePath = Path.Combine(Path.GetTempPath(), "test_source_" + Guid.NewGuid() + ".package");
        await File.WriteAllBytesAsync(sourcePath, new byte[100]);

        var classResId = new PackageResourceId(TextureTypeIds.Ts3DdsTexture, 0, 0x1111111111111111UL);
        var entryResId = new PackageResourceId(TextureTypeIds.Ts3DdsTexture, 0, 0x2222222222222222UL);

        var entry = new PackageResourceEntry(entryResId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new TextureResourceClassification(classResId, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS Image Texture", GameVersion.Sims3, Array.Empty<ConversionIssue>());

        var request = new TextureResourceExtractRequest(sourcePath, entry, classification, Path.GetTempPath());

        try
        {
            // Act
            var result = await _extractor.ExtractAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("TEXE004");
            _packageExporter.ExportCalled.Should().BeFalse("Package exporter MUST NOT be called when classification ResourceId does not match entry Id");
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task ExportAsync_DefaultAllowOverwriteIsFalse_PassesAllowOverwriteToPackageExporter()
    {
        // Arrange
        string sourcePath = Path.Combine(Path.GetTempPath(), "test_source_" + Guid.NewGuid() + ".package");
        string outputDir = Path.GetTempPath();
        await File.WriteAllBytesAsync(sourcePath, new byte[100]);

        var resId = new PackageResourceId(TextureTypeIds.Ts3DdsTexture, 0, 0x123456789ABCDEF0UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new TextureResourceClassification(resId, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS Image Texture", GameVersion.Sims3, Array.Empty<ConversionIssue>());

        _packageExporter.ResultToReturn = new PackageResourceExportResult(
            IsSuccess: true,
            SourcePackagePath: sourcePath,
            OutputFilePath: Path.Combine(outputDir, "00B2D882_00000000_123456789ABCDEF0.dds"),
            ExportedBytes: 500,
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new TextureResourceExtractRequest(sourcePath, entry, classification, outputDir, AllowOverwrite: false);

        try
        {
            // Act
            var result = await _extractor.ExtractAsync(request);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _packageExporter.ExportCalled.Should().BeTrue();
            _packageExporter.LastRequest.Should().NotBeNull();
            _packageExporter.LastRequest!.AllowOverwrite.Should().BeFalse("AllowOverwrite flag MUST be propagated to underlying PackageResourceExportRequest");
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task ExportAsync_UnknownResource_ReturnsControlledFailureWithoutDelegating()
    {
        // Arrange
        string sourcePath = Path.Combine(Path.GetTempPath(), "test_source_" + Guid.NewGuid() + ".package");
        await File.WriteAllBytesAsync(sourcePath, new byte[100]);

        var resId = new PackageResourceId(0x99999999u, 0, 0x1234);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = TextureResourceClassification.UnknownResource(resId, "TEXC001", "Unknown type");

        var request = new TextureResourceExtractRequest(sourcePath, entry, classification, Path.GetTempPath());

        try
        {
            // Act
            var result = await _extractor.ExtractAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("TEXE001");
            _packageExporter.ExportCalled.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task ExportAsync_SameSourceAndTargetPath_ReturnsControlledFailure()
    {
        // Arrange
        string sourcePath = Path.Combine(Path.GetTempPath(), "same_path.dds");
        await File.WriteAllBytesAsync(sourcePath, new byte[100]);

        var resId = new PackageResourceId(TextureTypeIds.Ts3DdsTexture, 0, 0x1234);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new TextureResourceClassification(resId, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS Image Texture", GameVersion.Sims3, Array.Empty<ConversionIssue>());

        var request = new TextureResourceExtractRequest(sourcePath, entry, classification, Path.GetTempPath(), CustomFileName: "same_path.dds");

        try
        {
            // Act
            var result = await _extractor.ExtractAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("TEXE008");
            _packageExporter.ExportCalled.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    [Theory]
    [InlineData(TextureTypeIds.Ts3DdsTexture, ".dds")]
    [InlineData(TextureTypeIds.Ts4Rle2Texture, ".rle2")]
    [InlineData(TextureTypeIds.Ts4LrleTexture, ".lrle")]
    [InlineData(TextureTypeIds.Ts4PngImage, ".png")]
    [InlineData(TextureTypeIds.Ts3SnapshotThumbnail, ".png")]
    [InlineData(TextureTypeIds.Ts4CasPartThumbnail, ".png")]
    [InlineData(0x99999999u, ".raw")]
    public void GetExtensionForTexture_ReturnsDeterministicFileExtension(uint typeId, string expectedExtension)
    {
        // Act
        string actualExtension = TextureResourceExtractor.GetExtensionForTexture(typeId);

        // Assert
        actualExtension.Should().Be(expectedExtension);
    }

    [Fact]
    public async Task ExportAsync_PackageExporterFailure_AggregatesIssuesInResult()
    {
        // Arrange
        string sourcePath = Path.Combine(Path.GetTempPath(), "test_source_" + Guid.NewGuid() + ".package");
        string outputDir = Path.GetTempPath();
        await File.WriteAllBytesAsync(sourcePath, new byte[100]);

        var resId = new PackageResourceId(TextureTypeIds.Ts3DdsTexture, 0, 0x123456789ABCDEF0UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);
        var classification = new TextureResourceClassification(resId, TextureClassificationKind.KnownTexture, TextureMapKind.Diffuse, "DDS Image Texture", GameVersion.Sims3, Array.Empty<ConversionIssue>());

        _packageExporter.ResultToReturn = new PackageResourceExportResult(
            IsSuccess: false,
            SourcePackagePath: sourcePath,
            OutputFilePath: Path.Combine(outputDir, "00B2D882_00000000_123456789ABCDEF0.dds"),
            ExportedBytes: 0,
            Issues: new[] { new ConversionIssue("EXPE005", "File already exists", ConversionIssueSeverity.Error) }
        );

        var request = new TextureResourceExtractRequest(sourcePath, entry, classification, outputDir);

        try
        {
            // Act
            var result = await _extractor.ExtractAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("EXPE005");
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    private sealed class StubPackageResourceExporter : IPackageResourceExporter
    {
        public bool ExportCalled { get; private set; }
        public PackageResourceExportRequest? LastRequest { get; private set; }
        public PackageResourceExportResult ResultToReturn { get; set; } = new(false, "", "", 0, Array.Empty<ConversionIssue>());

        public Task<PackageResourceExportResult> ExportAsync(PackageResourceExportRequest request, CancellationToken cancellationToken = default)
        {
            ExportCalled = true;
            LastRequest = request;
            return Task.FromResult(ResultToReturn);
        }
    }
}
