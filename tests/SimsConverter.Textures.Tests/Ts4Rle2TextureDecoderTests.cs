using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using FluentAssertions;
using SimsConverter.Domain.Enums;
using SimsConverter.Textures.Services;
using Xunit;

namespace SimsConverter.Textures.Tests;

public class Ts4Rle2TextureDecoderTests
{
    private readonly Ts4Rle2TexturePayloadBuilder _builder = new();
    private readonly Ts4Rle2TextureDecoder _decoder = new();

    [Fact]
    public void Decode_ValidRle2Payload_DecodesMipLevelsAndBuildsValidDds()
    {
        // Arrange: 32x32 DXT5 DDS with 2 mipmaps
        byte[] originalDds = CreateValidDdsWithPixels(32, 32, "DXT5", mipCount: 2, blockSize: 16);
        var buildResult = _builder.BuildPayload(originalDds, "TestKey");
        buildResult.IsSuccess.Should().BeTrue();
        byte[] rle2Payload = buildResult.Payload!;

        // Act
        var decodeResult = _decoder.Decode(rle2Payload, "TestKey");

        // Assert
        decodeResult.IsSuccess.Should().BeTrue();
        decodeResult.Width.Should().Be(32);
        decodeResult.Height.Should().Be(32);
        decodeResult.MipMapCount.Should().Be(2);
        decodeResult.FormatKind.Should().Be(DdsTextureFormatKind.Dxt5);
        decodeResult.MipLevels.Should().HaveCount(2);

        decodeResult.MipLevels![0].Width.Should().Be(32);
        decodeResult.MipLevels[0].Height.Should().Be(32);
        decodeResult.MipLevels[0].BlockCount.Should().Be(64); // 8x8 = 64 blocks

        decodeResult.MipLevels[1].Width.Should().Be(16);
        decodeResult.MipLevels[1].Height.Should().Be(16);
        decodeResult.MipLevels[1].BlockCount.Should().Be(16); // 4x4 = 16 blocks

        // Assert 1:1 round-trip pixel data equality
        decodeResult.DecodedDdsPayload.Should().Equal(originalDds);
    }

    [Fact]
    public void Decode_Dxt1AndDxt5_PreservesFormatAndMipMetrics()
    {
        // Arrange
        byte[] dxt1Dds = CreateValidDdsWithPixels(64, 64, "DXT1", mipCount: 3, blockSize: 8);
        byte[] dxt5Dds = CreateValidDdsWithPixels(64, 64, "DXT5", mipCount: 3, blockSize: 16);

        byte[] rle2Dxt1 = _builder.BuildPayload(dxt1Dds, "Dxt1Key").Payload!;
        byte[] rle2Dxt5 = _builder.BuildPayload(dxt5Dds, "Dxt5Key").Payload!;

        // Act
        var decDxt1 = _decoder.Decode(rle2Dxt1, "Dxt1Key");
        var decDxt5 = _decoder.Decode(rle2Dxt5, "Dxt5Key");

        // Assert
        decDxt1.IsSuccess.Should().BeTrue();
        decDxt1.FormatKind.Should().Be(DdsTextureFormatKind.Dxt1);
        decDxt1.DecodedDdsPayload.Should().Equal(dxt1Dds);

        decDxt5.IsSuccess.Should().BeTrue();
        decDxt5.FormatKind.Should().Be(DdsTextureFormatKind.Dxt5);
        decDxt5.DecodedDdsPayload.Should().Equal(dxt5Dds);
    }

    [Fact]
    public void Decode_InvalidMagicSignature_ReturnsRLE2001Issue()
    {
        // Arrange
        byte[] dds = CreateValidDdsWithPixels(16, 16, "DXT1", mipCount: 1, blockSize: 8);
        byte[] rle2Payload = _builder.BuildPayload(dds, "MagicKey").Payload!;
        // Corrupt magic bytes
        rle2Payload[0] = (byte)'X';

        // Act
        var result = _decoder.Decode(rle2Payload, "MagicKey");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "RLE2001");
    }

    [Fact]
    public void Decode_InvalidVersion_ReturnsRLE2001Issue()
    {
        // Arrange
        byte[] dds = CreateValidDdsWithPixels(16, 16, "DXT1", mipCount: 1, blockSize: 8);
        byte[] rle2Payload = _builder.BuildPayload(dds, "VersionKey").Payload!;
        // Corrupt version to 99
        BinaryPrimitives.WriteUInt16LittleEndian(rle2Payload.AsSpan(4, 2), 99);

        // Act
        var result = _decoder.Decode(rle2Payload, "VersionKey");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "RLE2001");
    }

    [Fact]
    public void Decode_TruncatedCommandStream_ReturnsRLE2004OrRLE2005Issue()
    {
        // Arrange
        byte[] dds = CreateValidDdsWithPixels(16, 16, "DXT1", mipCount: 1, blockSize: 8);
        byte[] rle2Payload = _builder.BuildPayload(dds, "TruncKey").Payload!;

        // Truncate payload in the middle of command/pixel stream
        byte[] truncatedPayload = rle2Payload.Take(rle2Payload.Length - 10).ToArray();

        // Act
        var result = _decoder.Decode(truncatedPayload, "TruncKey");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "RLE2004" || i.Code == "RLE2005");
    }

    [Fact]
    public void Decode_CorruptedCommandStreamBlockCountMismatch_ReturnsRLE2006Issue()
    {
        // Arrange
        byte[] dds = CreateValidDdsWithPixels(16, 16, "DXT1", mipCount: 1, blockSize: 8);
        byte[] rle2Payload = _builder.BuildPayload(dds, "MismatchKey").Payload!;

        // Mip 0 for 16x16 has 16 blocks (command 0x8F = 15 + 1 = 16 blocks).
        // Let's modify command byte to 0x85 (only 6 blocks decoded, causing mismatch with expected 16 blocks)
        int cmdOffset = 14 + 16; // header (14) + 1 mip table entry (16)
        rle2Payload[cmdOffset] = 0x85;

        // Act
        var result = _decoder.Decode(rle2Payload, "MismatchKey");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "RLE2006");
    }

    private static byte[] CreateValidDdsWithPixels(uint width, uint height, string fourCc, uint mipCount, int blockSize)
    {
        int headerSize = 128;
        int totalPixelBytes = 0;

        for (uint i = 0; i < mipCount; i++)
        {
            int shift = (int)Math.Min(i, 31);
            uint mipW = Math.Max(1, width >> shift);
            uint mipH = Math.Max(1, height >> shift);

            uint blocksX = Math.Max(1, (mipW + 3) / 4);
            uint blocksY = Math.Max(1, (mipH + 3) / 4);
            uint totalBlocks = blocksX * blocksY;

            totalPixelBytes += (int)(totalBlocks * (uint)blockSize);
        }

        byte[] buffer = new byte[headerSize + totalPixelBytes];

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x00, 4), 0x20534444); // "DDS "
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x04, 4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x08, 4), 0x00021007);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x0C, 4), height);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x10, 4), width);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x1C, 4), mipCount);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x4C, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x50, 4), 0x04);
        byte[] fourCcBytes = Encoding.ASCII.GetBytes(fourCc.PadRight(4, '\0'));
        Array.Copy(fourCcBytes, 0, buffer, 0x54, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x6C, 4), 0x1000);

        for (int i = headerSize; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i % 251);
        }

        return buffer;
    }
}
