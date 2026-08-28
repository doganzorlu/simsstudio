using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record Sims3PackExportResult(
    bool IsSuccess,
    string SourceSims3PackPath,
    string OutputFilePath,
    long ExportedBytes,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static Sims3PackExportResult Failure(string sourcePath, string outputPath, string issueCode, string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Error);
        return new Sims3PackExportResult(
            false,
            sourcePath,
            outputPath,
            0,
            new[] { issue }
        );
    }
}
