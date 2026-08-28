using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record PackageDetectionResult(
    PackageContainerKind ContainerKind,
    GameVersion DetectedGameVersion,
    PackageDetectionConfidence Confidence,
    string MajorVersion = "0",
    string MinorVersion = "0",
    int IndexEntryCount = 0,
    IReadOnlyList<ConversionIssue>? Issues = null
)
{
    public IReadOnlyList<ConversionIssue> Issues { get; init; } = Issues ?? Array.Empty<ConversionIssue>();

    public static PackageDetectionResult Unknown(string issueCode, string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Warning);
        return new PackageDetectionResult(
            PackageContainerKind.Unknown,
            GameVersion.Unknown,
            PackageDetectionConfidence.None,
            "0",
            "0",
            0,
            new[] { issue }
        );
    }
}
