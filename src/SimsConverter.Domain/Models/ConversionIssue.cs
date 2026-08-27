using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record ConversionIssue(
    string Code,
    string Message,
    ConversionIssueSeverity Severity,
    GameVersion TargetVersion = GameVersion.Unknown
);
