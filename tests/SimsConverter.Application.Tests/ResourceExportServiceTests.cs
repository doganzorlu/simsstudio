using System;
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

public class ResourceExportServiceTests
{
    [Fact]
    public async Task ExportResourceAsync_SanitizesFormattedKeyToFilename_AndCallsExporter()
    {
        // Arrange
        var fakeExporter = new FakeResourceExporter();
        var service = new ResourceExportService(fakeExporter);

        var row = new PackageResourceRow(
            0x00B2D882u,
            0x00000000u,
            0x123456789ABCDEF0UL,
            "0x00B2D882",
            "0x00000000",
            "0x123456789ABCDEF0",
            "00B2D882:00000000:123456789ABCDEF0",
            100,
            64,
            64,
            PackageCompressionKind.None,
            "None"
        );

        string destDir = Path.GetTempPath();
        var request = new SingleResourceExportRequest("/path/source.package", row, destDir, AllowOverwrite: false);

        // Act
        var result = await service.ExportResourceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        fakeExporter.LastRequest.Should().NotBeNull();
        fakeExporter.LastRequest!.OutputFilePath.Should().EndWith(Path.Combine(destDir, "00B2D882_00000000_123456789ABCDEF0.raw"));
        fakeExporter.LastRequest.Offset.Should().Be(100);
        fakeExporter.LastRequest.CompressedSize.Should().Be(64);
        fakeExporter.LastRequest.AllowOverwrite.Should().BeFalse();
    }

    [Theory]
    [InlineData("00B2D882:00000000:123456789ABCDEF0", "00B2D882_00000000_123456789ABCDEF0.raw")]
    [InlineData("../sub/folder/file.bin", "__sub_folder_file.bin.raw")]
    [InlineData("invalid:name*with?chars", "invalid_name_with_chars.raw")]
    [InlineData("   ", "unnamed_resource.raw")]
    [InlineData("", "unnamed_resource.raw")]
    public void SanitizeFilename_ShouldProduceSafeFilename(string rawInput, string expectedFilename)
    {
        // Act
        string result = ResourceExportService.SanitizeFilename(rawInput);

        // Assert
        result.Should().Be(expectedFilename);
    }

    [Fact]
    public async Task ExportResourceAsync_GivenNullResource_ReturnsControlledFailure()
    {
        // Arrange
        var fakeExporter = new FakeResourceExporter();
        var service = new ResourceExportService(fakeExporter);

        var request = new SingleResourceExportRequest("/path/source.package", null!, Path.GetTempPath());

        // Act
        var result = await service.ExportResourceAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("EXPE010");
    }

    private sealed class FakeResourceExporter : IPackageResourceExporter
    {
        public PackageResourceExportRequest? LastRequest { get; private set; }

        public Task<PackageResourceExportResult> ExportAsync(PackageResourceExportRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var result = new PackageResourceExportResult(true, request.SourcePackagePath, request.OutputFilePath, request.CompressedSize, Array.Empty<ConversionIssue>());
            return Task.FromResult(result);
        }
    }
}
