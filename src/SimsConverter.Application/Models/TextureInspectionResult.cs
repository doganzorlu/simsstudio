using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record TextureInspectionResult(
    bool IsSuccess,
    string PackageFilePath,
    IReadOnlyList<TextureResourceRow> Rows,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static TextureInspectionResult Failure(string packageFilePath, string code, string message)
    {
        var issue = new ConversionIssue(code, message, ConversionIssueSeverity.Error);
        return new TextureInspectionResult(
            IsSuccess: false,
            PackageFilePath: packageFilePath,
            Rows: Array.Empty<TextureResourceRow>(),
            Issues: new[] { issue }
        );
    }
}
