using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record DdsPayloadValidationResult(
    bool IsSuccess,
    ulong ExpectedPayloadBytes,
    ulong ActualPayloadBytes,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static DdsPayloadValidationResult Failure(ulong expectedBytes, ulong actualBytes, string code, string message)
    {
        var issue = new ConversionIssue(code, message, ConversionIssueSeverity.Error);
        return new DdsPayloadValidationResult(false, expectedBytes, actualBytes, new[] { issue });
    }
}
