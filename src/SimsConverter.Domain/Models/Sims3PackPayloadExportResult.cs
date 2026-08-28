using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record Sims3PackPayloadExportResult(
    bool IsSuccess,
    string SourceSims3PackPath,
    string OutputFilePath,
    long ExportedBytes,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static Sims3PackPayloadExportResult Failure(
        string sourcePath,
        string outputPath,
        string issueCode,
        string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Error);
        return new Sims3PackPayloadExportResult(
            false,
            sourcePath,
            outputPath,
            0,
            new[] { issue }
        );
    }
}
