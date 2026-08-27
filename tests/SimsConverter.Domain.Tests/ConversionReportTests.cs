using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Domain.Tests;

public class ConversionReportTests
{
    [Fact]
    public void NewReport_ShouldHaveZeroIssues_AndNoErrors()
    {
        var report = new ConversionReport(GameVersion.Sims3, GameVersion.Sims4);

        report.SourceVersion.Should().Be(GameVersion.Sims3);
        report.TargetVersion.Should().Be(GameVersion.Sims4);
        report.Issues.Should().BeEmpty();
        report.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void AddIssue_WithError_ShouldSetHasErrorsTrue()
    {
        var report = new ConversionReport(GameVersion.Sims3, GameVersion.Sims4);
        var issue = new ConversionIssue("ERR001", "Invalid mesh format", ConversionIssueSeverity.Error, GameVersion.Sims4);

        report.AddIssue(issue);

        report.Issues.Should().ContainSingle();
        report.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void AddIssue_WithWarningOnly_ShouldNotSetHasErrorsTrue()
    {
        var report = new ConversionReport(GameVersion.Sims3, GameVersion.Sims4);
        var issue = new ConversionIssue("WARN001", "Missing texture map", ConversionIssueSeverity.Warning, GameVersion.Sims4);

        report.AddIssue(issue);

        report.Issues.Should().ContainSingle();
        report.HasErrors.Should().BeFalse();
    }
}
