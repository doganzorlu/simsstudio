using System;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Textures.Tests;

public class DdsPayloadValidatorTests
{
    private readonly DdsPayloadValidator _validator = new();

    [Fact]
    public void Validate_Dxt1SingleMip4x4_Returns8BytesExpectedPayload()
    {
        // Arrange: 4x4 DXT1 single mipmap
        var metadata = new DdsTextureMetadata(4, 4, 1, DdsTextureFormatKind.Dxt1, "DXT1", 0x04, 0, 0x1000);

        // Act
        var result = _validator.Validate(metadata, actualPayloadBytes: 8);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ExpectedPayloadBytes.Should().Be(8);
        result.ActualPayloadBytes.Should().Be(8);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_Dxt5SingleMip4x4_Returns16BytesExpectedPayload()
    {
        // Arrange: 4x4 DXT5 single mipmap
        var metadata = new DdsTextureMetadata(4, 4, 1, DdsTextureFormatKind.Dxt5, "DXT5", 0x04, 0, 0x1000);

        // Act
        var result = _validator.Validate(metadata, actualPayloadBytes: 16);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ExpectedPayloadBytes.Should().Be(16);
        result.ActualPayloadBytes.Should().Be(16);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_Dxt1MipChain_CalculatesCorrectTotalBytes()
    {
        // Arrange: 16x16 DXT1 with 3 mips (16x16=128B, 8x8=32B, 4x4=8B -> Total = 168B)
        var metadata = new DdsTextureMetadata(16, 16, 3, DdsTextureFormatKind.Dxt1, "DXT1", 0x04, 0, 0x1000);

        // Act
        var result = _validator.Validate(metadata, actualPayloadBytes: 168);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ExpectedPayloadBytes.Should().Be(168);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_Uncompressed32bppRgba_CalculatesCorrectBytes()
    {
        // Arrange: 4x4 uncompressed 32bpp RGBA single mip (4*32+7)/8 * 4 = 64B
        var metadata = new DdsTextureMetadata(4, 4, 1, DdsTextureFormatKind.UncompressedRgba, "", 0x40, 32, 0x1000);

        // Act
        var result = _validator.Validate(metadata, actualPayloadBytes: 64);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ExpectedPayloadBytes.Should().Be(64);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ActualPayloadShorterThanExpected_ProducesTEXV001Error()
    {
        // Arrange: 16x16 DXT1 expecting 168B, but actual payload is only 100B
        var metadata = new DdsTextureMetadata(16, 16, 3, DdsTextureFormatKind.Dxt1, "DXT1", 0x04, 0, 0x1000);

        // Act
        var result = _validator.Validate(metadata, actualPayloadBytes: 100);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExpectedPayloadBytes.Should().Be(168);
        result.ActualPayloadBytes.Should().Be(100);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXV001");
    }

    [Fact]
    public void Validate_UnknownFormat_ProducesTEXV004WarningAndFailureStatus()
    {
        // Arrange: Unknown DDS format family "FOO1"
        var metadata = new DdsTextureMetadata(512, 512, 1, DdsTextureFormatKind.Unknown, "FOO1", 0, 0, 0x1000);

        // Act
        var result = _validator.Validate(metadata, actualPayloadBytes: 5000);

        // Assert
        result.IsSuccess.Should().BeFalse("Unknown format payload size validation cannot be confirmed as successful");
        result.ExpectedPayloadBytes.Should().Be(0);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXV004");
    }

    [Fact]
    public void Validate_ExcessiveMipCount_ReturnsTEXV005ErrorQuicklyWithoutLooping()
    {
        // Arrange: 4x4 texture (max valid mips is 3), but header declares MipMapCount = 100
        var metadata = new DdsTextureMetadata(4, 4, 100, DdsTextureFormatKind.Dxt1, "DXT1", 0x04, 0, 0x1000);

        // Act
        var result = _validator.Validate(metadata, actualPayloadBytes: 8);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXV005");
        result.Issues[0].Message.Should().Contain("exceeds maximum valid mip count");
    }

    [Fact]
    public void Validate_FullDdsPayloadSpan_CalculatesActualBytesFromHeaderOffset()
    {
        // Arrange: 128-byte header + 8-byte payload = 136 bytes total
        byte[] fullPayload = new byte[128 + 8];
        var metadata = new DdsTextureMetadata(4, 4, 1, DdsTextureFormatKind.Dxt1, "DXT1", 0x04, 0, 0x1000);

        // Act
        var result = _validator.Validate(metadata, fullPayload);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ActualPayloadBytes.Should().Be(8, "128-byte header MUST be subtracted from total file length");
        result.ExpectedPayloadBytes.Should().Be(8);
    }

    [Fact]
    public void Validate_OverflowDimensions_ProducesTEXV003Error()
    {
        // Arrange: Extreme width/height that causes integer overflow when squared/multiplied (with valid mip count 1)
        var metadata = new DdsTextureMetadata(uint.MaxValue, uint.MaxValue, 1, DdsTextureFormatKind.UncompressedRgba, "", 0x40, 32, 0x1000);

        // Act
        var result = _validator.Validate(metadata, actualPayloadBytes: 1000);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXV003");
    }
}
