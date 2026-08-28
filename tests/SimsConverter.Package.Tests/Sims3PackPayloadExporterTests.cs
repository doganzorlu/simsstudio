using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Package.Tests;

public class Sims3PackPayloadExporterTests
{
    private readonly Sims3PackPayloadExporter _exporter = new();

    private static byte[] CreateSims3PackWithDbpfFixture(byte[] dbpfBytes)
    {
        byte[] sigBytes = Encoding.ASCII.GetBytes("TS3Pack\0");
        uint sigLen = (uint)sigBytes.Length;
        ushort version = 1;
        byte[] xmlBytes = Encoding.UTF8.GetBytes("<Sims3Pack/>");
        uint xmlLen = (uint)xmlBytes.Length;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(sigLen);      // DWORD
        writer.Write(sigBytes);    // 8 bytes
        writer.Write(version);     // WORD
        writer.Write(xmlLen);      // DWORD
        writer.Write(xmlBytes);    // XML payload
        writer.Write(dbpfBytes);   // DBPF payload

        return ms.ToArray();
    }

    [Fact]
    public async Task ExportAsync_GivenValidDbpfCatalogEntry_ExportsPackageFileStartingWithDbpfMagic()
    {
        // Arrange: Valid DBPF payload (128 bytes)
        byte[] dbpfPayload = new byte[128];
        "DBPF"u8.ToArray().CopyTo(dbpfPayload, 0);

        string sourcePath = Path.Combine(Path.GetTempPath(), "valid_source_" + Guid.NewGuid() + ".sims3pack");
        string outputPath = Path.Combine(Path.GetTempPath(), "exported_target_" + Guid.NewGuid() + ".package");

        byte[] fullSourceBytes = CreateSims3PackWithDbpfFixture(dbpfPayload);
        await File.WriteAllBytesAsync(sourcePath, fullSourceBytes);

        int archiveOffset = 4 + 8 + 2 + 4 + Encoding.UTF8.GetByteCount("<Sims3Pack/>");
        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: 1,
            Kind: Sims3PackPayloadKind.DbpfPackage,
            DataOffset: archiveOffset,
            EstimatedSizeBytes: dbpfPayload.Length,
            DisplayName: "Test Package",
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackPayloadExportRequest(sourcePath, catalogEntry, outputPath);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.ExportedBytes.Should().Be(dbpfPayload.Length);
            File.Exists(outputPath).Should().BeTrue();

            byte[] exportedHeader = new byte[4];
            await using (var fs = File.OpenRead(outputPath))
            {
                await fs.ReadExactlyAsync(exportedHeader, 0, 4);
            }
            Encoding.ASCII.GetString(exportedHeader).Should().Be("DBPF");
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenEstimatedSizeBytesNull_ExportsToEofSuccessfully()
    {
        // Arrange: EstimatedSizeBytes = null (export from DataOffset to EOF)
        byte[] dbpfPayload = new byte[128];
        "DBPF"u8.ToArray().CopyTo(dbpfPayload, 0);

        string sourcePath = Path.Combine(Path.GetTempPath(), "null_size_source_" + Guid.NewGuid() + ".sims3pack");
        string outputPath = Path.Combine(Path.GetTempPath(), "null_size_target_" + Guid.NewGuid() + ".package");

        byte[] fullSourceBytes = CreateSims3PackWithDbpfFixture(dbpfPayload);
        await File.WriteAllBytesAsync(sourcePath, fullSourceBytes);

        int archiveOffset = 4 + 8 + 2 + 4 + Encoding.UTF8.GetByteCount("<Sims3Pack/>");
        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: 1,
            Kind: Sims3PackPayloadKind.DbpfPackage,
            DataOffset: archiveOffset,
            EstimatedSizeBytes: null, // Null estimated size
            DisplayName: "Test Null Size Package",
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackPayloadExportRequest(sourcePath, catalogEntry, outputPath);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.ExportedBytes.Should().Be(dbpfPayload.Length);
            File.Exists(outputPath).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenPngCatalogEntryRequest_ReturnsControlledFailure()
    {
        // Arrange: Catalog entry with Kind = PngPreview
        string sourcePath = Path.Combine(Path.GetTempPath(), "png_source_" + Guid.NewGuid() + ".sims3pack");
        string outputPath = Path.Combine(Path.GetTempPath(), "output_" + Guid.NewGuid() + ".png");

        await File.WriteAllBytesAsync(sourcePath, new byte[100]);

        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: 1,
            Kind: Sims3PackPayloadKind.PngPreview,
            DataOffset: 20,
            EstimatedSizeBytes: 50,
            DisplayName: "Test PNG",
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackPayloadExportRequest(sourcePath, catalogEntry, outputPath);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PE001");
            File.Exists(outputPath).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenSourcePathEqualsOutputPath_ReturnsControlledFailure()
    {
        // Arrange: Output path identical to source path
        string sourcePath = Path.Combine(Path.GetTempPath(), "same_path_" + Guid.NewGuid() + ".sims3pack");
        await File.WriteAllBytesAsync(sourcePath, new byte[100]);

        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: 1,
            Kind: Sims3PackPayloadKind.DbpfPackage,
            DataOffset: 10,
            EstimatedSizeBytes: 50,
            DisplayName: "Test Package",
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackPayloadExportRequest(sourcePath, catalogEntry, sourcePath, AllowOverwrite: true);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PE008");
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenExistingOutputFileAndAllowOverwriteFalse_ReturnsControlledFailureAndLeavesFileUntouched()
    {
        // Arrange: Existing output file with AllowOverwrite = false
        string sourcePath = Path.Combine(Path.GetTempPath(), "source_" + Guid.NewGuid() + ".sims3pack");
        string outputPath = Path.Combine(Path.GetTempPath(), "existing_out_" + Guid.NewGuid() + ".package");

        byte[] existingContent = Encoding.ASCII.GetBytes("EXISTING_UNTOUCHED_CONTENT");
        await File.WriteAllBytesAsync(sourcePath, new byte[100]);
        await File.WriteAllBytesAsync(outputPath, existingContent);

        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: 1,
            Kind: Sims3PackPayloadKind.DbpfPackage,
            DataOffset: 10,
            EstimatedSizeBytes: 50,
            DisplayName: "Test Package",
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackPayloadExportRequest(sourcePath, catalogEntry, outputPath, AllowOverwrite: false);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PE005");

            byte[] remainingContent = await File.ReadAllBytesAsync(outputPath);
            remainingContent.Should().Equal(existingContent);
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenExistingOutputFileAndAllowOverwriteTrue_Succeeds()
    {
        // Arrange: Existing output file with AllowOverwrite = true
        byte[] dbpfPayload = new byte[128];
        "DBPF"u8.ToArray().CopyTo(dbpfPayload, 0);

        string sourcePath = Path.Combine(Path.GetTempPath(), "source_ow_" + Guid.NewGuid() + ".sims3pack");
        string outputPath = Path.Combine(Path.GetTempPath(), "existing_ow_" + Guid.NewGuid() + ".package");

        byte[] fullSourceBytes = CreateSims3PackWithDbpfFixture(dbpfPayload);
        await File.WriteAllBytesAsync(sourcePath, fullSourceBytes);
        await File.WriteAllBytesAsync(outputPath, new byte[10]);

        int archiveOffset = 4 + 8 + 2 + 4 + Encoding.UTF8.GetByteCount("<Sims3Pack/>");
        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: 1,
            Kind: Sims3PackPayloadKind.DbpfPackage,
            DataOffset: archiveOffset,
            EstimatedSizeBytes: dbpfPayload.Length,
            DisplayName: "Test Package",
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackPayloadExportRequest(sourcePath, catalogEntry, outputPath, AllowOverwrite: true);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeTrue();
            File.Exists(outputPath).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenInvalidDataOffsetBeyondStreamLength_ReturnsControlledFailure()
    {
        // Arrange: DataOffset beyond file length
        string sourcePath = Path.Combine(Path.GetTempPath(), "source_bad_offset_" + Guid.NewGuid() + ".sims3pack");
        string outputPath = Path.Combine(Path.GetTempPath(), "output_bad_offset_" + Guid.NewGuid() + ".package");

        await File.WriteAllBytesAsync(sourcePath, new byte[50]);

        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: 1,
            Kind: Sims3PackPayloadKind.DbpfPackage,
            DataOffset: 9999, // Way beyond file length 50
            EstimatedSizeBytes: 100,
            DisplayName: "Bad Offset Package",
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackPayloadExportRequest(sourcePath, catalogEntry, outputPath);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PE006");
            File.Exists(outputPath).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenZeroOrNegativeEstimatedSizeBytes_ReturnsControlledFailure()
    {
        // Arrange: EstimatedSizeBytes = -10
        string sourcePath = Path.Combine(Path.GetTempPath(), "source_negative_size_" + Guid.NewGuid() + ".sims3pack");
        string outputPath = Path.Combine(Path.GetTempPath(), "output_negative_size_" + Guid.NewGuid() + ".package");

        await File.WriteAllBytesAsync(sourcePath, new byte[50]);

        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: 1,
            Kind: Sims3PackPayloadKind.DbpfPackage,
            DataOffset: 10,
            EstimatedSizeBytes: -10,
            DisplayName: "Negative Size Package",
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackPayloadExportRequest(sourcePath, catalogEntry, outputPath);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PE004");
            File.Exists(outputPath).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenExportedPayloadNotStartingWithDbpf_CleansUpTempFileAndReturnsControlledFailure()
    {
        // Arrange: Payload starting with junk bytes instead of DBPF magic
        byte[] junkPayload = new byte[128];
        "JUNK"u8.ToArray().CopyTo(junkPayload, 0);

        string sourcePath = Path.Combine(Path.GetTempPath(), "source_junk_" + Guid.NewGuid() + ".sims3pack");
        string outputPath = Path.Combine(Path.GetTempPath(), "output_junk_" + Guid.NewGuid() + ".package");

        byte[] fullSourceBytes = CreateSims3PackWithDbpfFixture(junkPayload);
        await File.WriteAllBytesAsync(sourcePath, fullSourceBytes);

        int archiveOffset = 4 + 8 + 2 + 4 + Encoding.UTF8.GetByteCount("<Sims3Pack/>");
        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: 1,
            Kind: Sims3PackPayloadKind.DbpfPackage,
            DataOffset: archiveOffset,
            EstimatedSizeBytes: junkPayload.Length,
            DisplayName: "Junk Package",
            Issues: Array.Empty<ConversionIssue>()
        );

        var request = new Sims3PackPayloadExportRequest(sourcePath, catalogEntry, outputPath);

        try
        {
            // Act
            var result = await _exporter.ExportAsync(request);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle();
            result.Issues[0].Code.Should().Be("S3PE007");
            File.Exists(outputPath).Should().BeFalse();

            // Verify zero leftover .tmp files exist in target directory for this operation
            string targetDir = Path.GetDirectoryName(outputPath)!;
            Directory.GetFiles(targetDir, $"{Path.GetFileName(outputPath)}.tmp.*").Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
}
