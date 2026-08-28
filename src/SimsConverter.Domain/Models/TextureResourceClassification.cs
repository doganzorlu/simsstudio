using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record TextureResourceClassification(
    PackageResourceId ResourceId,
    TextureClassificationKind Classification,
    TextureMapKind MapKind,
    string FormatName,
    GameVersion DetectedGameVersion,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static TextureResourceClassification UnknownResource(PackageResourceId id, string issueCode, string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Warning);
        return new TextureResourceClassification(
            id,
            TextureClassificationKind.Unknown,
            TextureMapKind.Unknown,
            "Unknown / Unclassified Format",
            GameVersion.Unknown,
            new[] { issue }
        );
    }
}
