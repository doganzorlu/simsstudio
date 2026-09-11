using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectPackageWritePlan(
    string SourcePackagePath,
    string TargetOutputPath,
    GameVersion TargetGameVersion,
    IReadOnlyList<DecorativeObjectPackageWriteResourceEntry> PlannedResources,
    bool IsPlanValid,
    IReadOnlyList<ConversionIssue> Issues,
    DecorativeObjectResourceSetReport? ResourceSetReport = null
);
