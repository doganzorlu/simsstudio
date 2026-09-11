using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Textures.Contracts;

public record DecodedRle2MipLevel(
    uint MipIndex,
    uint Width,
    uint Height,
    uint BlockCount,
    byte[] PixelData);

public record Ts4Rle2DecodeResult(
    bool IsSuccess,
    byte[]? DecodedDdsPayload,
    uint Width,
    uint Height,
    uint MipMapCount,
    DdsTextureFormatKind FormatKind,
    IReadOnlyList<DecodedRle2MipLevel>? MipLevels,
    IReadOnlyList<ConversionIssue> Issues)
{
    public static Ts4Rle2DecodeResult Failure(string code, string message, IReadOnlyList<ConversionIssue>? existingIssues = null)
    {
        var issues = new List<ConversionIssue>();
        if (existingIssues != null)
        {
            issues.AddRange(existingIssues);
        }
        issues.Add(new ConversionIssue(code, message, ConversionIssueSeverity.Error));

        return new Ts4Rle2DecodeResult(
            IsSuccess: false,
            DecodedDdsPayload: null,
            Width: 0,
            Height: 0,
            MipMapCount: 0,
            FormatKind: DdsTextureFormatKind.Unknown,
            MipLevels: null,
            Issues: issues.AsReadOnly()
        );
    }
}

public interface ITs4Rle2TextureDecoder
{
    Ts4Rle2DecodeResult Decode(ReadOnlySpan<byte> rle2Payload, string resourceKey);
}
