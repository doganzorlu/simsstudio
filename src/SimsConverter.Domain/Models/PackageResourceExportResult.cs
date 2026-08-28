using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record PackageResourceExportResult(
    bool IsSuccess,
    string SourcePackagePath,
    string OutputFilePath,
    long ExportedBytes,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static PackageResourceExportResult Failure(
        string sourcePackagePath,
        string outputFilePath,
        string issueCode,
        string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Error);
        return new PackageResourceExportResult(
            false,
            sourcePackagePath ?? string.Empty,
            outputFilePath ?? string.Empty,
            0,
            new[] { issue }
        );
    }
}
