using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Textures.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Textures.Tests;

public class DdsHeaderParserTests
{
    private readonly DdsHeaderParser _parser = new();

    [Fact]
    public void Parse_ValidDxt1Header_ReturnsMetadataWithDxt1FormatKind()
    {
        // Arrange
        byte[] header = CreateValidDdsHeader(1024, 512, "DXT1", mipMaps: 4);

        // Act
        var result = _parser.Parse(header);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata!.Width.Should().Be(1024);
        result.Metadata.Height.Should().Be(512);
        result.Metadata.MipMapCount.Should().Be(4);
        result.Metadata.FormatKind.Should().Be(DdsTextureFormatKind.Dxt1);
        result.Metadata.FourCC.Should().Be("DXT1");
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ValidDxt5Header_ReturnsMetadataWithDxt5FormatKind()
    {
        // Arrange
        byte[] header = CreateValidDdsHeader(2048, 2048, "DXT5", mipMaps: 8);

        // Act
        var result = _parser.Parse(header);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata!.Width.Should().Be(2048);
        result.Metadata.Height.Should().Be(2048);
        result.Metadata.MipMapCount.Should().Be(8);
        result.Metadata.FormatKind.Should().Be(DdsTextureFormatKind.Dxt5);
        result.Metadata.FourCC.Should().Be("DXT5");
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Parse_InvalidMagicSignature_ReturnsTEXD002Issue()
    {
        // Arrange
        byte[] header = CreateValidDdsHeader(512, 512, "DXT1");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), 0x12345678);

        // Act
        var result = _parser.Parse(header);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Metadata.Should().BeNull();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXD002");
    }

    [Fact]
    public void Parse_InvalidHeaderDwSize_ReturnsTEXD005Issue()
    {
        // Arrange: Invalid dwSize (e.g. 100 instead of 124)
        byte[] header = CreateValidDdsHeader(512, 512, "DXT1");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x04, 4), 100);

        // Act
        var result = _parser.Parse(header);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Metadata.Should().BeNull();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXD005");
    }

    [Fact]
    public void Parse_InvalidPixelFormatDwSize_ReturnsTEXD006Issue()
    {
        // Arrange: Invalid ddspf.dwSize (e.g. 16 instead of 32)
        byte[] header = CreateValidDdsHeader(512, 512, "DXT1");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x4C, 4), 16);

        // Act
        var result = _parser.Parse(header);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Metadata.Should().BeNull();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXD006");
    }

    [Fact]
    public void Parse_TruncatedHeader_ReturnsTEXD001Issue()
    {
        // Arrange: 64-byte payload (less than required 128 bytes)
        byte[] truncatedData = new byte[64];

        // Act
        var result = _parser.Parse(truncatedData);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Metadata.Should().BeNull();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXD001");
    }

    [Fact]
    public void Parse_ZeroWidthOrHeight_ReturnsTEXD003Issue()
    {
        // Arrange: Zero width
        byte[] zeroWidthHeader = CreateValidDdsHeader(0, 512, "DXT1");

        // Act
        var result = _parser.Parse(zeroWidthHeader);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Metadata.Should().BeNull();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXD003");
    }

    [Fact]
    public void Parse_UnknownFourCC_ReturnsUnknownFormatKindWithTEXD004Issue()
    {
        // Arrange: Unknown FourCC "FOO1"
        byte[] header = CreateValidDdsHeader(512, 512, "FOO1");

        // Act
        var result = _parser.Parse(header);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata!.FormatKind.Should().Be(DdsTextureFormatKind.Unknown);
        result.Metadata.FourCC.Should().Be("FOO1");
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXD004");
    }

    [Fact]
    public async Task ParseAsync_StreamInput_PreservesStreamPosition()
    {
        // Arrange
        byte[] header = CreateValidDdsHeader(256, 256, "DXT1");
        using var stream = new MemoryStream(header);
        stream.Position = 0;

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        stream.Position.Should().Be(0, "Stream position MUST be preserved after header parsing");
    }

    private static byte[] CreateValidDdsHeader(uint width, uint height, string fourCc, uint pfFlags = 0x04, uint mipMaps = 1)
    {
        byte[] buffer = new byte[128];

        // Magic "DDS " (0x20534444) at 0x00
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x00, 4), 0x20534444);

        // dwSize (124) at 0x04
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x04, 4), 124);

        // dwHeight at 0x0C
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x0C, 4), height);

        // dwWidth at 0x10
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x10, 4), width);

        // dwMipMapCount at 0x1C
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x1C, 4), mipMaps);

        // ddspf.dwSize (32) at 0x4C
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x4C, 4), 32);

        // ddspf.dwFlags (0x04 = DDPF_FOURCC) at 0x50
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x50, 4), pfFlags);

        // ddspf.dwFourCC at 0x54
        byte[] fourCcBytes = Encoding.ASCII.GetBytes(fourCc.PadRight(4, '\0'));
        Array.Copy(fourCcBytes, 0, buffer, 0x54, 4);

        // dwCaps (0x1000 = DDSCAPS_TEXTURE) at 0x6C
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x6C, 4), 0x1000);

        return buffer;
    }
}
