using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public class ConversionReport
{
    private readonly List<ConversionIssue> _issues = new();

    public GameVersion SourceVersion { get; }
    public GameVersion TargetVersion { get; }
    public DateTime CreatedAtUtc { get; }
    public IReadOnlyCollection<ConversionIssue> Issues => _issues.AsReadOnly();

    public bool HasErrors => _issues.Exists(i => i.Severity is ConversionIssueSeverity.Error or ConversionIssueSeverity.Fatal);

    public ConversionReport(GameVersion sourceVersion, GameVersion targetVersion)
    {
        SourceVersion = sourceVersion;
        TargetVersion = targetVersion;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void AddIssue(ConversionIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        _issues.Add(issue);
    }
}
