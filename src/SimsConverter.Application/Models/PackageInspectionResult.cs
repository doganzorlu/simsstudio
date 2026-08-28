using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record PackageInspectionResult(
    bool IsSuccess,
    string FilePath,
    DbpfHeader? Header,
    IReadOnlyList<PackageResourceRow> Resources,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static PackageInspectionResult Failure(string filePath, string issueCode, string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Error);
        return new PackageInspectionResult(
            false,
            filePath ?? string.Empty,
            null,
            Array.Empty<PackageResourceRow>(),
            new[] { issue }
        );
    }
}
