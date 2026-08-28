using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Contracts;

namespace SimsConverter.Textures.Services;

public class DdsPayloadValidator : IDdsPayloadValidator
{
    private const ulong DdsHeaderSizeBytes = 128;

    public DdsPayloadValidationResult Validate(DdsTextureMetadata metadata, ulong actualPayloadBytes)
    {
        if (metadata == null)
        {
            return DdsPayloadValidationResult.Failure(0, actualPayloadBytes, "TEXV000", "DDS texture metadata is null.");
        }

        if (metadata.FormatKind == DdsTextureFormatKind.Unknown)
        {
            var warningIssue = new ConversionIssue(
                "TEXV004",
                $"Cannot calculate exact expected payload size for unknown DDS format family '{metadata.FourCC}'.",
                ConversionIssueSeverity.Warning
            );
            return new DdsPayloadValidationResult(false, 0, actualPayloadBytes, new[] { warningIssue });
        }

        uint maxAllowedMips = GetMaxMeaningfulMipCount(metadata.Width, metadata.Height);
        if (metadata.MipMapCount > maxAllowedMips)
        {
            return DdsPayloadValidationResult.Failure(
                0,
                actualPayloadBytes,
                "TEXV005",
                $"DDS header MipMapCount ({metadata.MipMapCount}) exceeds maximum valid mip count ({maxAllowedMips}) for dimensions {metadata.Width}x{metadata.Height}."
            );
        }

        ulong expectedBytes;
        try
        {
            expectedBytes = CalculateExpectedPayloadBytes(metadata);
        }
        catch (OverflowException)
        {
            return DdsPayloadValidationResult.Failure(0, actualPayloadBytes, "TEXV003", "Texture dimensions or mipmap count caused integer overflow during size calculation.");
        }

        if (actualPayloadBytes < expectedBytes)
        {
            return DdsPayloadValidationResult.Failure(
                expectedBytes,
                actualPayloadBytes,
                "TEXV001",
                $"Actual DDS payload size ({actualPayloadBytes} bytes) is less than expected minimum payload size ({expectedBytes} bytes)."
            );
        }

        return new DdsPayloadValidationResult(
            IsSuccess: true,
            ExpectedPayloadBytes: expectedBytes,
            ActualPayloadBytes: actualPayloadBytes,
            Issues: Array.Empty<ConversionIssue>()
        );
    }

    public DdsPayloadValidationResult Validate(DdsTextureMetadata metadata, ReadOnlySpan<byte> fullDdsPayload)
    {
        ulong actualPayloadBytes = fullDdsPayload.Length > (int)DdsHeaderSizeBytes
            ? (ulong)(fullDdsPayload.Length - (int)DdsHeaderSizeBytes)
            : 0;
        return Validate(metadata, actualPayloadBytes);
    }

    public static uint GetMaxMeaningfulMipCount(uint width, uint height)
    {
        uint maxDim = Math.Max(width, height);
        if (maxDim == 0) return 1;
        return (uint)Math.Floor(Math.Log2(maxDim)) + 1;
    }

    public static ulong CalculateExpectedPayloadBytes(DdsTextureMetadata metadata)
    {
        if (metadata == null) return 0;

        uint width = metadata.Width;
        uint height = metadata.Height;
        uint mips = metadata.MipMapCount > 0 ? metadata.MipMapCount : 1;

        ulong totalBytes = 0;

        checked
        {
            for (uint i = 0; i < mips; i++)
            {
                int shift = (int)Math.Min(i, 31);
                uint mipWidth = Math.Max(1, width >> shift);
                uint mipHeight = Math.Max(1, height >> shift);

                ulong levelBytes = CalculateLevelBytes(metadata.FormatKind, mipWidth, mipHeight, metadata.RgbBitCount);
                totalBytes = checked(totalBytes + levelBytes);
            }
        }

        return totalBytes;
    }

    private static ulong CalculateLevelBytes(DdsTextureFormatKind formatKind, uint width, uint height, uint rgbBitCount)
    {
        checked
        {
            switch (formatKind)
            {
                case DdsTextureFormatKind.Dxt1:
                case DdsTextureFormatKind.Ati1:
                    {
                        ulong blocksX = (ulong)Math.Max(1, (width + 3) / 4);
                        ulong blocksY = (ulong)Math.Max(1, (height + 3) / 4);
                        return blocksX * blocksY * 8;
                    }

                case DdsTextureFormatKind.Dxt3:
                case DdsTextureFormatKind.Dxt5:
                case DdsTextureFormatKind.Ati2:
                    {
                        ulong blocksX = (ulong)Math.Max(1, (width + 3) / 4);
                        ulong blocksY = (ulong)Math.Max(1, (height + 3) / 4);
                        return blocksX * blocksY * 16;
                    }

                case DdsTextureFormatKind.UncompressedRgba:
                    {
                        uint bpp = rgbBitCount > 0 ? rgbBitCount : 32;
                        ulong pitch = ((ulong)width * bpp + 7) / 8;
                        return pitch * (ulong)height;
                    }

                default:
                    return 0;
            }
        }
    }
}
