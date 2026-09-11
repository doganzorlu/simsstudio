using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

/// <summary>
/// Domain model representing the full bidirectional conversion capability evaluation matrix for a package.
/// </summary>
public record DecorativeObjectConversionCapabilityMatrix(
    GameVersion SourceGameVersion,
    GameVersion TargetGameVersion,
    int TotalResourcesAnalyzed,
    int SupportedResourceCount,
    int PassThroughResourceCount,
    int UnsupportedResourceCount,
    IReadOnlyList<ConversionCapabilityEntry> Entries,
    IReadOnlyList<ConversionIssue> Issues,
    bool IsConversionFeasible
)
{
    public static DecorativeObjectConversionCapabilityMatrix Empty(GameVersion sourceVersion, GameVersion targetVersion) =>
        new DecorativeObjectConversionCapabilityMatrix(
            SourceGameVersion: sourceVersion,
            TargetGameVersion: targetVersion,
            TotalResourcesAnalyzed: 0,
            SupportedResourceCount: 0,
            PassThroughResourceCount: 0,
            UnsupportedResourceCount: 0,
            Entries: Array.Empty<ConversionCapabilityEntry>(),
            Issues: Array.Empty<ConversionIssue>(),
            IsConversionFeasible: true
        );
}
