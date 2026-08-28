using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record Sims3PackInspectionResult(
    bool IsSuccess,
    string FilePath,
    string? RootElementName,
    string? DeclaredEncoding,
    long? RawXmlSizeBytes,
    string? Title,
    string? AssetId,
    string? AssetType,
    string? Description,
    IReadOnlyList<Sims3PackPayloadRow> PayloadRows,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static Sims3PackInspectionResult Failure(string filePath, string issueCode, string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Error);
        return new Sims3PackInspectionResult(
            false,
            filePath,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Array.Empty<Sims3PackPayloadRow>(),
            new[] { issue }
        );
    }
}
