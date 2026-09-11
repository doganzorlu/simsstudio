using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Textures.Contracts;

public record Ts4Rle2PayloadResult(
    bool IsSuccess,
    byte[]? Payload,
    uint Width,
    uint Height,
    uint MipMapCount,
    ushort Rle2Format,
    IReadOnlyList<ConversionIssue> Issues)
{
    public static Ts4Rle2PayloadResult Failure(string code, string message, IReadOnlyList<ConversionIssue>? existingIssues = null)
    {
        var issues = new List<ConversionIssue>();
        if (existingIssues != null)
        {
            issues.AddRange(existingIssues);
        }
        issues.Add(new ConversionIssue(code, message, ConversionIssueSeverity.Error));

        return new Ts4Rle2PayloadResult(
            IsSuccess: false,
            Payload: null,
            Width: 0,
            Height: 0,
            MipMapCount: 0,
            Rle2Format: 0,
            Issues: issues.AsReadOnly()
        );
    }
}

public interface ITs4Rle2TexturePayloadBuilder
{
    Ts4Rle2PayloadResult BuildPayload(ReadOnlySpan<byte> ddsPayload, string resourceKey);
}
