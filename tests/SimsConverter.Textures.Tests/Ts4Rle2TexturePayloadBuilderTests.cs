using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using FluentAssertions;
using SimsConverter.Textures.Services;
using Xunit;

namespace SimsConverter.Textures.Tests;

public class Ts4Rle2TexturePayloadBuilderTests
{
    private readonly Ts4Rle2TexturePayloadBuilder _builder = new();

    [Fact]
    public void BuildPayload_ValidDxt1Dds_ProducesValidRle2Payload()
    {
        // Arrange: 16x16 DXT1 DDS with 2 mipmaps
        // Mip 0: 16x16 -> 4x4 blocks = 16 blocks * 8 bytes = 128 bytes
        // Mip 1: 8x8 -> 2x2 blocks = 4 blocks * 8 bytes = 32 bytes
        // Total pixel bytes = 160
        byte[] ddsPayload = CreateValidDdsWithPixels(16, 16, "DXT1", mipCount: 2, blockSize: 8);

        // Act
        var result = _builder.BuildPayload(ddsPayload, "TestDxt1Key");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Payload.Should().NotBeNull();
        result.Width.Should().Be(16);
        result.Height.Should().Be(16);
        result.MipMapCount.Should().Be(2);
        result.Rle2Format.Should().Be(0x0000); // DXT1

        byte[] payload = result.Payload!;
        Encoding.ASCII.GetString(payload, 0, 4).Should().Be("RLE2");
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(4, 2)).Should().Be(2); // Version 2
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(6, 2)).Should().Be(16); // Width
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(8, 2)).Should().Be(16); // Height
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(10, 2)).Should().Be(2); // MipMapCount
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(12, 2)).Should().Be(0); // Format
    }

    [Fact]
    public void BuildPayload_ValidDxt5Dds_ProducesValidRle2Payload()
    {
        // Arrange: 32x32 DXT5 DDS with 1 mipmap
        // 32x32 -> 8x8 = 64 blocks * 16 bytes = 1024 bytes
        byte[] ddsPayload = CreateValidDdsWithPixels(32, 32, "DXT5", mipCount: 1, blockSize: 16);

        // Act
        var result = _builder.BuildPayload(ddsPayload, "TestDxt5Key");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Payload.Should().NotBeNull();
        result.Width.Should().Be(32);
        result.Height.Should().Be(32);
        result.MipMapCount.Should().Be(1);
        result.Rle2Format.Should().Be(0x0001); // DXT5
    }

    [Fact]
    public void BuildPayload_ValidAti1AndAti2Dds_ProducesValidRle2Payloads()
    {
        // Arrange
        byte[] ati1Dds = CreateValidDdsWithPixels(16, 16, "ATI1", mipCount: 1, blockSize: 8);
        byte[] ati2Dds = CreateValidDdsWithPixels(16, 16, "ATI2", mipCount: 1, blockSize: 16);

        // Act
        var res1 = _builder.BuildPayload(ati1Dds, "Ati1Key");
        var res2 = _builder.BuildPayload(ati2Dds, "Ati2Key");

        // Assert
        res1.IsSuccess.Should().BeTrue();
        res1.Rle2Format.Should().Be(0x0002); // ATI1

        res2.IsSuccess.Should().BeTrue();
        res2.Rle2Format.Should().Be(0x0003); // ATI2
    }

    [Fact]
    public void BuildPayload_SameDdsInput_ProducesByteForByteDeterministicOutput()
    {
        // Arrange
        byte[] ddsPayload = CreateValidDdsWithPixels(64, 64, "DXT5", mipCount: 3, blockSize: 16);

        // Act
        var res1 = _builder.BuildPayload(ddsPayload, "DetKey");
        var res2 = _builder.BuildPayload(ddsPayload, "DetKey");

        // Assert
        res1.IsSuccess.Should().BeTrue();
        res2.IsSuccess.Should().BeTrue();
        res1.Payload.Should().Equal(res2.Payload);
    }

    [Fact]
    public void BuildPayload_TruncatedDdsData_ReturnsFailureIssue()
    {
        // Arrange: Header claims 2 mips but buffer is truncated
        byte[] ddsPayload = CreateValidDdsWithPixels(16, 16, "DXT1", mipCount: 2, blockSize: 8);
        byte[] truncatedPayload = ddsPayload.Take(128 + 30).ToArray(); // Less than 128 + 160

        // Act
        var result = _builder.BuildPayload(truncatedPayload, "TruncatedKey");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Payload.Should().BeNull();
        result.Issues.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildPayload_UnsupportedFourCC_ReturnsFailureIssue()
    {
        // Arrange: Unknown FourCC "FOO1"
        byte[] ddsHeader = CreateValidDdsWithPixels(16, 16, "FOO1", mipCount: 1, blockSize: 8);

        // Act
        var result = _builder.BuildPayload(ddsHeader, "UnknownFourCCKey");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Payload.Should().BeNull();
        result.Issues.Should().NotBeEmpty();
        result.Issues.Should().Contain(i => i.Code == "TEXR002" || i.Code == "TEXD004" || i.Code == "TEXR003");
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

        // Magic "DDS " (0x20534444) at 0x00
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x00, 4), 0x20534444);

        // dwSize (124) at 0x04
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x04, 4), 124);

        // dwHeight at 0x0C
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x0C, 4), height);

        // dwWidth at 0x10
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x10, 4), width);

        // dwMipMapCount at 0x1C
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x1C, 4), mipCount);

        // ddspf.dwSize (32) at 0x4C
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x4C, 4), 32);

        // ddspf.dwFlags (0x04 = DDPF_FOURCC) at 0x50
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x50, 4), 0x04);

        // ddspf.dwFourCC at 0x54
        byte[] fourCcBytes = Encoding.ASCII.GetBytes(fourCc.PadRight(4, '\0'));
        Array.Copy(fourCcBytes, 0, buffer, 0x54, 4);

        // dwCaps (0x1000 = DDSCAPS_TEXTURE) at 0x6C
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x6C, 4), 0x1000);

        // Fill deterministic dummy pixel pattern
        for (int i = headerSize; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i % 251);
        }

        return buffer;
    }
}
