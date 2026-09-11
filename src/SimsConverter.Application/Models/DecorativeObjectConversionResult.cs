using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectConversionResult(
    bool IsSuccess,
    string SourcePackagePath,
    string TargetOutputPath,
    DecorativeObjectConversionPlan? Plan,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static DecorativeObjectConversionResult Failure(
        string sourcePath,
        string targetPath,
        string errorCode,
        string message,
        IReadOnlyList<ConversionIssue>? existingIssues = null)
    {
        var issuesList = new List<ConversionIssue>();
        if (existingIssues != null)
        {
            issuesList.AddRange(existingIssues);
        }
        issuesList.Add(new ConversionIssue(errorCode, message, ConversionIssueSeverity.Error));

        return new DecorativeObjectConversionResult(
            IsSuccess: false,
            SourcePackagePath: sourcePath ?? string.Empty,
            TargetOutputPath: targetPath ?? string.Empty,
            Plan: null,
            Issues: issuesList.AsReadOnly()
        );
    }
}
