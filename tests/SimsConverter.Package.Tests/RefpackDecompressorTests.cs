using System;
using System.Text;
using FluentAssertions;
using SimsConverter.Package.Services;
using Xunit;

namespace SimsConverter.Package.Tests;

public class RefpackDecompressorTests
{
    [Fact]
    public void TryDecompress_WithValid3ByteHeaderRefpackStream_DecompressesSuccessfully()
    {
        // Arrange: 3-byte header RefPack stream for "ABCD" (4 bytes decompressed)
        byte[] compressed = new byte[] { 0x10, 0xFB, 0x00, 0x00, 0x04, 0xE0, (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0xFC };

        // Act
        bool success = RefpackDecompressor.TryDecompress(compressed, 4, out byte[] decompressed);

        // Assert
        success.Should().BeTrue("Valid RefPack payload with matching expected size must decompress successfully.");
        decompressed.Should().Equal(Encoding.ASCII.GetBytes("ABCD"));
    }

    [Fact]
    public void TryDecompress_WithValid4ByteHeaderRefpackStream_DecompressesSuccessfully()
    {
        // Arrange: 4-byte header RefPack stream for "WXYZ" (4 bytes decompressed)
        byte[] compressed = new byte[] { 0x90, 0xFB, 0x00, 0x00, 0x00, 0x04, 0xE0, (byte)'W', (byte)'X', (byte)'Y', (byte)'Z', 0xFC };

        // Act
        bool success = RefpackDecompressor.TryDecompress(compressed, 4, out byte[] decompressed);

        // Assert
        success.Should().BeTrue("Valid RefPack payload with 4-byte header must decompress successfully.");
        decompressed.Should().Equal(Encoding.ASCII.GetBytes("WXYZ"));
    }

    [Fact]
    public void TryDecompress_WithValidCopyOpcodeRefpackStream_DecompressesHistoryCorrectly()
    {
        // Arrange: "ABCD" literal + copy 'D' 3 times + 'E' stop literal = "ABCDDDDE" (8 bytes)
        byte[] compressed = new byte[]
        {
            0x10, 0xFB, 0x00, 0x00, 0x08, // Header: decompressed size = 8
            0xE0, (byte)'A', (byte)'B', (byte)'C', (byte)'D', // 4 literals: "ABCD"
            0x00, 0x00, // 2-byte copy opcode: literal=0, copy=3, offset=1 ("DDD")
            0xFD, (byte)'E' // Stop opcode with 1 literal: "E"
        };

        // Act
        bool success = RefpackDecompressor.TryDecompress(compressed, 8, out byte[] decompressed);

        // Assert
        success.Should().BeTrue();
        Encoding.ASCII.GetString(decompressed).Should().Be("ABCDDDDE");
    }

    [Fact]
    public void TryDecompress_WhenHeaderSizeAndExpectedDecompressedSizeMismatch_ReturnsFalse()
    {
        // Arrange: Header states decompressed size = 4 ("ABCD"), but expectedDecompressedSize = 10
        byte[] compressed = new byte[] { 0x10, 0xFB, 0x00, 0x00, 0x04, 0xE0, (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0xFC };

        // Act
        bool success = RefpackDecompressor.TryDecompress(compressed, 10, out byte[] decompressed);

        // Assert
        success.Should().BeFalse("Decompressor must enforce strict equality between header size and expected size.");
        decompressed.Should().BeEmpty();
    }

    [Fact]
    public void TryDecompress_WhenStreamEndsPrematurelyBeforeHeaderDecompressedSize_ReturnsFalse()
    {
        // Arrange: Header states size = 8, but stream ends after 4 bytes without completing decompressed buffer
        byte[] truncatedCompressed = new byte[] { 0x10, 0xFB, 0x00, 0x00, 0x08, 0xE0, (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0xFC };

        // Act
        bool success = RefpackDecompressor.TryDecompress(truncatedCompressed, 8, out byte[] decompressed);

        // Assert
        success.Should().BeFalse("Decompressor must fail when outPos does not reach header decompressed size.");
        decompressed.Should().BeEmpty();
    }

    [Fact]
    public void TryDecompress_WithInvalidMagic_ReturnsFalse()
    {
        // Arrange: Invalid magic header (0x12 0x34)
        byte[] invalidMagicData = new byte[] { 0x12, 0x34, 0x00, 0x00, 0x04, 0xE0, (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0xFC };

        // Act
        bool success = RefpackDecompressor.TryDecompress(invalidMagicData, 4, out byte[] decompressed);

        // Assert
        success.Should().BeFalse("Non-RefPack magic header must be rejected.");
        decompressed.Should().BeEmpty();
    }

    [Fact]
    public void IsRefpackHeader_ValidAndInvalidHeaders_EvaluatesCorrectly()
    {
        RefpackDecompressor.IsRefpackHeader(new byte[] { 0x10, 0xFB }).Should().BeTrue();
        RefpackDecompressor.IsRefpackHeader(new byte[] { 0x90, 0xFB }).Should().BeTrue();
        RefpackDecompressor.IsRefpackHeader(new byte[] { 0xFB, 0x10 }).Should().BeTrue();
        RefpackDecompressor.IsRefpackHeader(new byte[] { 0xFB, 0x90 }).Should().BeTrue();

        RefpackDecompressor.IsRefpackHeader(new byte[] { 0x00, 0xFB }).Should().BeFalse();
        RefpackDecompressor.IsRefpackHeader(new byte[] { 0x78, 0x9C }).Should().BeFalse();
        RefpackDecompressor.IsRefpackHeader(new byte[] { 0x12, 0x34 }).Should().BeFalse();
        RefpackDecompressor.IsRefpackHeader(Array.Empty<byte>()).Should().BeFalse();
    }

    [Fact]
    public void TryDecompress_With4ByteHeaderMagicOrderFb90_DecompressesSuccessfully()
    {
        // Arrange: 4-byte header with 0xFB 0x90 header order for "WXYZ"
        byte[] compressed = new byte[] { 0xFB, 0x90, 0x00, 0x00, 0x00, 0x04, 0xE0, (byte)'W', (byte)'X', (byte)'Y', (byte)'Z', 0xFC };

        // Act
        bool success = RefpackDecompressor.TryDecompress(compressed, 4, out byte[] decompressed);

        // Assert
        success.Should().BeTrue();
        decompressed.Should().Equal(Encoding.ASCII.GetBytes("WXYZ"));
    }

    [Fact]
    public void TryDecompress_WithNullOrUndersizedBuffer_ReturnsFalse()
    {
        // Act & Assert
        RefpackDecompressor.TryDecompress(null!, 10, out byte[] d1).Should().BeFalse();
        RefpackDecompressor.TryDecompress(Array.Empty<byte>(), 10, out byte[] d2).Should().BeFalse();
        RefpackDecompressor.TryDecompress(new byte[] { 0x10, 0xFB, 0x00 }, 10, out byte[] d3).Should().BeFalse();
    }
}
