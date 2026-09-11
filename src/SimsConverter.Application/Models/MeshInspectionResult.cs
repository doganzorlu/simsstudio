using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record MeshInspectionResult(
    bool IsSuccess,
    string PackageFilePath,
    IReadOnlyList<MeshResourceRow> Rows,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static MeshInspectionResult Failure(string filePath, string errorCode, string message)
    {
        var issues = new[] { new ConversionIssue(errorCode, message, ConversionIssueSeverity.Error) };
        return new MeshInspectionResult(
            IsSuccess: false,
            PackageFilePath: filePath ?? string.Empty,
            Rows: Array.Empty<MeshResourceRow>(),
            Issues: issues
        );
    }
}
