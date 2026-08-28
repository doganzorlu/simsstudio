using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Package.Tests;

public class PackageDetectorTests
{
    private readonly PackageDetector _detector = new();

    [Fact]
    public void Detect_GivenValidDbpfHeader_ReturnsDbpfContainer_AndUnknownGameVersion()
    {
        // Arrange: 96-byte DBPF header layout with DBPF magic, major 2, minor 0, index count 5
        byte[] header = new byte[96];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(header, 0);
        BitConverter.GetBytes(2).CopyTo(header, 4); // Major version 2
        BitConverter.GetBytes(0).CopyTo(header, 8); // Minor version 0
        BitConverter.GetBytes(5).CopyTo(header, 24); // Index count 5

        // Act
        var result = _detector.Detect(header);

        // Assert
        result.ContainerKind.Should().Be(PackageContainerKind.Dbpf);
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        result.Confidence.Should().Be(PackageDetectionConfidence.Low);
        result.MajorVersion.Should().Be("2");
        result.MinorVersion.Should().Be("0");
        result.IndexEntryCount.Should().Be(5);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Detect_GivenInvalidMagic_ReturnsUnknownContainer_AndUnknownGameVersion()
    {
        // Arrange
        byte[] header = Encoding.ASCII.GetBytes("INVALID_HEADER_BYTES_1234567890");

        // Act
        var result = _detector.Detect(header);

        // Assert
        result.ContainerKind.Should().Be(PackageContainerKind.Unknown);
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        result.Confidence.Should().Be(PackageDetectionConfidence.None);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PKG002");
    }

    [Fact]
    public void Detect_GivenEmptyInput_ReturnsSafeUnknownResult_WithoutCrashing()
    {
        // Arrange
        byte[] emptyBuffer = Array.Empty<byte>();

        // Act
        var result = _detector.Detect(emptyBuffer);

        // Assert
        result.ContainerKind.Should().Be(PackageContainerKind.Unknown);
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        result.Confidence.Should().Be(PackageDetectionConfidence.None);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PKG001");
    }

    [Fact]
    public void Detect_GivenTruncatedInput_ReturnsSafeResult_WithoutCrashing()
    {
        // Arrange: 2 bytes only (truncated magic)
        byte[] truncatedBuffer = new byte[] { 0x44, 0x42 }; // "DB"

        // Act
        var result = _detector.Detect(truncatedBuffer);

        // Assert
        result.ContainerKind.Should().Be(PackageContainerKind.Unknown);
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        result.Confidence.Should().Be(PackageDetectionConfidence.None);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PKG001");
    }

    [Fact]
    public void Detect_GivenTruncatedDbpfHeader_ReturnsDbpfWithWarningIssue()
    {
        // Arrange: DBPF magic present, but buffer is only 16 bytes (shorter than standard 96 bytes)
        byte[] shortDbpfBuffer = new byte[16];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(shortDbpfBuffer, 0);
        BitConverter.GetBytes(2).CopyTo(shortDbpfBuffer, 4);

        // Act
        var result = _detector.Detect(shortDbpfBuffer);

        // Assert
        result.ContainerKind.Should().Be(PackageContainerKind.Dbpf);
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        result.Confidence.Should().Be(PackageDetectionConfidence.Low);
        result.MajorVersion.Should().Be("2");
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PKG003");
        result.Issues[0].Severity.Should().Be(ConversionIssueSeverity.Warning);
    }

    [Fact]
    public async Task DetectFileAsync_GivenNonExistentFile_ReturnsSafeUnknownResult()
    {
        // Arrange
        string nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_file_" + Guid.NewGuid() + ".package");

        // Act
        var result = await _detector.DetectFileAsync(nonExistentPath);

        // Assert
        result.ContainerKind.Should().Be(PackageContainerKind.Unknown);
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        result.Confidence.Should().Be(PackageDetectionConfidence.None);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PKG007");
    }

    [Fact]
    public async Task DetectFileAsync_GivenNullOrEmptyPath_ReturnsSafeUnknownResult()
    {
        // Act
        var nullResult = await _detector.DetectFileAsync(null);
        var emptyResult = await _detector.DetectFileAsync("   ");

        // Assert
        nullResult.ContainerKind.Should().Be(PackageContainerKind.Unknown);
        nullResult.Issues[0].Code.Should().Be("PKG006");

        emptyResult.ContainerKind.Should().Be(PackageContainerKind.Unknown);
        emptyResult.Issues[0].Code.Should().Be("PKG006");
    }

    [Fact]
    public async Task DetectFileAsync_GivenValidDbpfFile_ReadsSuccessfully_AndLeavesSourceFileIntact()
    {
        // Arrange: Create a temporary dummy DBPF file
        string tempFilePath = Path.Combine(Path.GetTempPath(), "test_package_" + Guid.NewGuid() + ".package");
        byte[] fileContent = new byte[128];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(fileContent, 0);
        BitConverter.GetBytes(2).CopyTo(fileContent, 4);
        BitConverter.GetBytes(0).CopyTo(fileContent, 8);
        BitConverter.GetBytes(10).CopyTo(fileContent, 24);

        await File.WriteAllBytesAsync(tempFilePath, fileContent);

        try
        {
            // Act
            var result = await _detector.DetectFileAsync(tempFilePath);

            // Assert
            result.ContainerKind.Should().Be(PackageContainerKind.Dbpf);
            result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
            result.Confidence.Should().Be(PackageDetectionConfidence.Low);
            result.MajorVersion.Should().Be("2");
            result.IndexEntryCount.Should().Be(10);

            // Verify source file still exists and content is unmodified
            File.Exists(tempFilePath).Should().BeTrue();
            byte[] readAfter = await File.ReadAllBytesAsync(tempFilePath);
            readAfter.Should().Equal(fileContent);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
