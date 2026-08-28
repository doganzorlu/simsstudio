using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record TextureResourceExtractResult(
    bool IsSuccess,
    string SourcePackagePath,
    string OutputFilePath,
    long ExportedBytes,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static TextureResourceExtractResult Failure(string sourcePath, string outputPath, string issueCode, string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Error);
        return new TextureResourceExtractResult(
            false,
            sourcePath,
            outputPath,
            0,
            new[] { issue }
        );
    }
}
