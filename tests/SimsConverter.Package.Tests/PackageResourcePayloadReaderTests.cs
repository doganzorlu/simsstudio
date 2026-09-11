using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Services;
using Xunit;

namespace SimsConverter.Package.Tests;

public class PackageResourcePayloadReaderTests
{
    [Fact]
    public void ReadPayload_NullFilePath_ReturnsFailureEXPR000()
    {
        var reader = new PackageResourcePayloadReader();
        var entry = new PackageResourceEntry(new PackageResourceId(1, 0, 1), 0, 10, 10, PackageCompressionKind.None, 0);
        var result = reader.ReadPayload(null!, entry);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "EXPR000");
    }

    [Fact]
    public void ReadPayload_UncompressedPayload_ReturnsRawBuffer()
    {
        var reader = new PackageResourcePayloadReader();
        string tempFile = Path.GetTempFileName();

        try
        {
            byte[] rawData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            File.WriteAllBytes(tempFile, rawData);

            var entry = new PackageResourceEntry(new PackageResourceId(1, 0, 1), 0, (uint)rawData.Length, (uint)rawData.Length, PackageCompressionKind.None, 0);
            var result = reader.ReadPayload(tempFile, entry);

            result.IsSuccess.Should().BeTrue();
            result.Payload.Should().Equal(rawData);
            result.Payload!.Length.Should().Be((int)entry.DecompressedSize);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadPayload_ZlibCompressedPayload_DecompressesAndVerifiesLengthMatchesDecompressedSize()
    {
        var reader = new PackageResourcePayloadReader();
        string tempFile = Path.GetTempFileName();

        try
        {
            byte[] originalData = new byte[100];
            for (int i = 0; i < originalData.Length; i++) originalData[i] = (byte)(i % 10);

            byte[] compressedData;
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                {
                    deflate.Write(originalData, 0, originalData.Length);
                }
                compressedData = ms.ToArray();
            }

            File.WriteAllBytes(tempFile, compressedData);

            var entry = new PackageResourceEntry(
                new PackageResourceId(1, 0, 1),
                0,
                (uint)compressedData.Length,
                (uint)originalData.Length,
                PackageCompressionKind.Zlib,
                0
            );

            var result = reader.ReadPayload(tempFile, entry);

            result.IsSuccess.Should().BeTrue();
            result.Payload.Should().NotBeNull();
            result.Payload!.Length.Should().Be((int)entry.DecompressedSize);
            result.Payload.Should().Equal(originalData);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadPayload_RefPackCompressedPayload_ReturnsFailurePKGP004()
    {
        var reader = new PackageResourcePayloadReader();
        string tempFile = Path.GetTempFileName();

        try
        {
            // RefPack header 0x10, 0xFB
            byte[] refpackData = new byte[] { 0x10, 0xFB, 0x00, 0x42, 0x1C, 0xE0, 0x03, 0x00 };
            File.WriteAllBytes(tempFile, refpackData);

            var entry = new PackageResourceEntry(
                new PackageResourceId(1, 0, 1),
                0,
                (uint)refpackData.Length,
                100,
                PackageCompressionKind.RefPack,
                0
            );

            var result = reader.ReadPayload(tempFile, entry);

            result.IsSuccess.Should().BeFalse();
            result.Issues.Should().ContainSingle(i => i.Code == "PKGP004");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
