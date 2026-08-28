using System;
using System.IO;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Package.Tests;

public class PackageResourceExporterTests
{
    private readonly PackageResourceExporter _exporter = new();

    [Fact]
    public async Task ExportAsync_GivenValidRequest_WritesExactBytes_AndLeavesSourceFileUnchanged()
    {
        // Arrange
        string sourcePackagePath = Path.Combine(Path.GetTempPath(), "source_export_test_" + Guid.NewGuid() + ".package");
        string outputFilePath = Path.Combine(Path.GetTempPath(), "output_export_test_" + Guid.NewGuid() + ".raw");

        byte[] sourceBuffer = new byte[256];
        new Random(42).NextBytes(sourceBuffer);

        long offset = 64;
        uint compressedSize = 32;
        byte[] expectedResourceBytes = new byte[compressedSize];
        Array.Copy(sourceBuffer, offset, expectedResourceBytes, 0, compressedSize);

        await File.WriteAllBytesAsync(sourcePackagePath, sourceBuffer);

        var request = new PackageResourceExportRequest(sourcePackagePath, offset, compressedSize, outputFilePath, AllowOverwrite: true);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.ExportedBytes.Should().Be(compressedSize);
            File.Exists(outputFilePath).Should().BeTrue();

            byte[] exportedBytes = await File.ReadAllBytesAsync(outputFilePath);
            exportedBytes.Should().Equal(expectedResourceBytes);

            // Source package check
            byte[] afterSourceBytes = await File.ReadAllBytesAsync(sourcePackagePath);
            afterSourceBytes.Should().Equal(sourceBuffer);
        }
        finally
        {
            if (File.Exists(sourcePackagePath)) File.Delete(sourcePackagePath);
            if (File.Exists(outputFilePath)) File.Delete(outputFilePath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenSameSourceAndOutputPath_ReturnsControlledError_AndLeavesSourceFileUnchanged()
    {
        // Arrange
        string sourcePackagePath = Path.Combine(Path.GetTempPath(), "source_same_path_" + Guid.NewGuid() + ".package");

        byte[] sourceBuffer = new byte[128];
        new Random(42).NextBytes(sourceBuffer);
        await File.WriteAllBytesAsync(sourcePackagePath, sourceBuffer);

        // Explicitly set OutputFilePath to SourcePackagePath with AllowOverwrite: true
        var request = new PackageResourceExportRequest(sourcePackagePath, Offset: 0, CompressedSize: 32, OutputFilePath: sourcePackagePath, AllowOverwrite: true);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("EXPE008");

            // Source file must be 100% unchanged
            byte[] currentSourceBytes = await File.ReadAllBytesAsync(sourcePackagePath);
            currentSourceBytes.Should().Equal(sourceBuffer);
        }
        finally
        {
            if (File.Exists(sourcePackagePath)) File.Delete(sourcePackagePath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenExistingFile_AndAllowOverwriteFalse_ReturnsControlledError()
    {
        // Arrange
        string sourcePackagePath = Path.Combine(Path.GetTempPath(), "source_exist_" + Guid.NewGuid() + ".package");
        string outputFilePath = Path.Combine(Path.GetTempPath(), "output_exist_" + Guid.NewGuid() + ".raw");

        await File.WriteAllBytesAsync(sourcePackagePath, new byte[128]);
        await File.WriteAllBytesAsync(outputFilePath, new byte[10]);

        var request = new PackageResourceExportRequest(sourcePackagePath, 0, 16, outputFilePath, AllowOverwrite: false);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("EXPE005");
        }
        finally
        {
            if (File.Exists(sourcePackagePath)) File.Delete(sourcePackagePath);
            if (File.Exists(outputFilePath)) File.Delete(outputFilePath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenOffsetExceedingFileSize_ReturnsControlledError()
    {
        // Arrange
        string sourcePackagePath = Path.Combine(Path.GetTempPath(), "source_overflow_" + Guid.NewGuid() + ".package");
        string outputFilePath = Path.Combine(Path.GetTempPath(), "output_overflow_" + Guid.NewGuid() + ".raw");

        await File.WriteAllBytesAsync(sourcePackagePath, new byte[64]);

        var request = new PackageResourceExportRequest(sourcePackagePath, Offset: 500, CompressedSize: 16, OutputFilePath: outputFilePath);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("EXPE006");
        }
        finally
        {
            if (File.Exists(sourcePackagePath)) File.Delete(sourcePackagePath);
            if (File.Exists(outputFilePath)) File.Delete(outputFilePath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenLongOverflowOffsetAndSize_ReturnsControlledErrorWithoutException()
    {
        // Arrange
        string sourcePackagePath = Path.Combine(Path.GetTempPath(), "source_long_overflow_" + Guid.NewGuid() + ".package");
        string outputFilePath = Path.Combine(Path.GetTempPath(), "output_long_overflow_" + Guid.NewGuid() + ".raw");

        await File.WriteAllBytesAsync(sourcePackagePath, new byte[64]);

        var request = new PackageResourceExportRequest(sourcePackagePath, Offset: long.MaxValue - 10, CompressedSize: 100, OutputFilePath: outputFilePath);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("EXPE006");
        }
        finally
        {
            if (File.Exists(sourcePackagePath)) File.Delete(sourcePackagePath);
            if (File.Exists(outputFilePath)) File.Delete(outputFilePath);
        }
    }
}
