using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record DdsParseResult(
    bool IsSuccess,
    DdsTextureMetadata? Metadata,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static DdsParseResult Failure(string code, string message)
    {
        var issue = new ConversionIssue(code, message, ConversionIssueSeverity.Error);
        return new DdsParseResult(false, null, new[] { issue });
    }
}
