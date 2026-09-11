using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Application.Tests;

public class PackageInspectionServiceTests
{
    private readonly DbpfPackageParser _parser = new();
    private readonly PackageInspectionService _service;

    public PackageInspectionServiceTests()
    {
        _service = new PackageInspectionService(_parser);
    }

    [Fact]
    public async Task InspectFileAsync_GivenValidPackageWithOneResource_ReturnsMappedResourceRow()
    {
        // Arrange: Temp DBPF package file with 1 resource
        string tempPath = Path.Combine(Path.GetTempPath(), "app_test_1res_" + Guid.NewGuid() + ".package");
        byte[] buffer = new byte[2000];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4); // Major 2
        BitConverter.GetBytes(1).CopyTo(buffer, 36); // 1 entry
        BitConverter.GetBytes(96).CopyTo(buffer, 40); // Index offset 96
        BitConverter.GetBytes(32).CopyTo(buffer, 44); // Index size 32

        // Resource Entry
        BitConverter.GetBytes(0x00B2D882u).CopyTo(buffer, 96 + 0); // TypeId
        BitConverter.GetBytes(0x00000000u).CopyTo(buffer, 96 + 4); // GroupId
        BitConverter.GetBytes(0x123456789ABCDEF0UL).CopyTo(buffer, 96 + 8); // InstanceId
        BitConverter.GetBytes(500u).CopyTo(buffer, 96 + 16); // DataOffset
        BitConverter.GetBytes(1024u).CopyTo(buffer, 96 + 20); // CompressedSize
        BitConverter.GetBytes(2048u).CopyTo(buffer, 96 + 24); // DecompressedSize
        BitConverter.GetBytes((ushort)0x5A42).CopyTo(buffer, 96 + 28); // Zlib

        await File.WriteAllBytesAsync(tempPath, buffer);

        try
        {
            // Act
            var result = await _service.InspectFileAsync(tempPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.FilePath.Should().Be(tempPath);
            result.Header.Should().NotBeNull();
            result.Header!.IndexEntryCount.Should().Be(1);

            result.Resources.Should().ContainSingle();
            var row = result.Resources[0];
            row.TypeId.Should().Be(0x00B2D882u);
            row.GroupId.Should().Be(0x00000000u);
            row.InstanceId.Should().Be(0x123456789ABCDEF0UL);
            row.TypeHex.Should().Be("0x00B2D882");
            row.GroupHex.Should().Be("0x00000000");
            row.InstanceHex.Should().Be("0x123456789ABCDEF0");
            row.FormattedKey.Should().Be("00B2D882:00000000:123456789ABCDEF0");
            row.Offset.Should().Be(500);
            row.CompressedSize.Should().Be(1024);
            row.DecompressedSize.Should().Be(2048);
            row.CompressionKind.Should().Be(PackageCompressionKind.Zlib);
            row.CompressionName.Should().Be("Zlib");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task InspectFileAsync_GivenMultipleResources_PreservesOrder()
    {
        // Arrange: DBPF package file with 2 resources
        string tempPath = Path.Combine(Path.GetTempPath(), "app_test_2res_" + Guid.NewGuid() + ".package");
        byte[] buffer = new byte[96 + 64];
        Encoding.ASCII.GetBytes("DBPF").CopyTo(buffer, 0);
        BitConverter.GetBytes(2).CopyTo(buffer, 4);
        BitConverter.GetBytes(2).CopyTo(buffer, 36);
        BitConverter.GetBytes(96).CopyTo(buffer, 40);
        BitConverter.GetBytes(64).CopyTo(buffer, 44);

        // Entry 0
        BitConverter.GetBytes(0x11111111u).CopyTo(buffer, 96 + 0);
        BitConverter.GetBytes(0x22222222u).CopyTo(buffer, 96 + 4);
        BitConverter.GetBytes(0x3333333333333333UL).CopyTo(buffer, 96 + 8);

        // Entry 1
        BitConverter.GetBytes(0x44444444u).CopyTo(buffer, 128 + 0);
        BitConverter.GetBytes(0x55555555u).CopyTo(buffer, 128 + 4);
        BitConverter.GetBytes(0x6666666666666666UL).CopyTo(buffer, 128 + 8);

        await File.WriteAllBytesAsync(tempPath, buffer);

        try
        {
            // Act
            var result = await _service.InspectFileAsync(tempPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Resources.Should().HaveCount(2);
            result.Resources[0].TypeId.Should().Be(0x11111111u);
            result.Resources[1].TypeId.Should().Be(0x44444444u);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task InspectFileAsync_GivenInvalidMagic_ReturnsControlledFailure_AndPreservesIssues()
    {
        // Arrange: Invalid magic file
        string tempPath = Path.Combine(Path.GetTempPath(), "app_test_invalid_" + Guid.NewGuid() + ".package");
        byte[] invalidBuffer = Encoding.ASCII.GetBytes("INVALID_PACKAGE_BYTES_123456");
        await File.WriteAllBytesAsync(tempPath, invalidBuffer);

        try
        {
            // Act
            var result = await _service.InspectFileAsync(tempPath);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Resources.Should().BeEmpty();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("PARSE002");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task InspectFileAsync_GivenNullOrEmptyFilePath_ReturnsControlledFailure()
    {
        // Act
        var nullResult = await _service.InspectFileAsync(null!);
        var emptyResult = await _service.InspectFileAsync("   ");

        // Assert
        nullResult.IsSuccess.Should().BeFalse();
        nullResult.Issues[0].Code.Should().Be("INSPECT001");

        emptyResult.IsSuccess.Should().BeFalse();
        emptyResult.Issues[0].Code.Should().Be("INSPECT001");
    }

    [Fact]
    public void ApplicationLayer_ShouldNotReferenceAvaloniaOrUIAssemblies()
    {
        // Arrange
        Assembly appAssembly = typeof(PackageInspectionService).Assembly;
        AssemblyName[] referencedAssemblies = appAssembly.GetReferencedAssemblies();

        // Assert: Ensure zero Avalonia UI references in SimsConverter.Application
        referencedAssemblies.Select(r => r.Name)
            .Should().NotContain(name => name != null && name.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase));
    }
}
