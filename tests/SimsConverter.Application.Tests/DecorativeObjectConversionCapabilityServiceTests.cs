using System;
using System.Collections.Generic;
using FluentAssertions;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Models;
using Xunit;

namespace SimsConverter.Application.Tests;

public class DecorativeObjectConversionCapabilityServiceTests
{
    private readonly DecorativeObjectConversionCapabilityService _service = new();

    private static PackageResourceRow CreateRow(uint typeId, ulong instanceId) =>
        PackageResourceRow.FromEntry(new PackageResourceEntry(new PackageResourceId(typeId, 0, instanceId), 0, 100, 100, PackageCompressionKind.None, 0));

    [Fact]
    public void EvaluateCapability_GivenEmptyPackageResult_ReturnsEmptyCapabilityMatrix()
    {
        // Arrange
        var packageResult = PackageInspectionResult.Failure("test.package", "TEST001", "Empty package");

        // Act
        var result = _service.EvaluateCapability(packageResult, GameVersion.Sims3, GameVersion.Sims4);

        // Assert
        result.Should().NotBeNull();
        result.TotalResourcesAnalyzed.Should().Be(0);
        result.SupportedResourceCount.Should().Be(0);
        result.Entries.Should().BeEmpty();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateCapability_GivenSupportedTs3Resources_ReturnsAllSupportedEntries()
    {
        // Arrange
        var resources = new List<PackageResourceRow>
        {
            CreateRow(Ts4ResourceTypeIds.CatalogObject, 1),
            CreateRow(Ts4ResourceTypeIds.ObjectDefinition, 2),
            CreateRow(Ts4ResourceTypeIds.Model, 3),
            CreateRow(Ts4ResourceTypeIds.ModelLod, 4),
            CreateRow(Ts4ResourceTypeIds.Geom, 5),
            CreateRow(0x00B2D882, 6)
        };

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "valid_ts3.package",
            Header: null,
            Resources: resources,
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var matrix = _service.EvaluateCapability(packageResult, GameVersion.Sims3, GameVersion.Sims4);

        // Assert
        matrix.TotalResourcesAnalyzed.Should().Be(6);
        matrix.SupportedResourceCount.Should().Be(6);
        matrix.PassThroughResourceCount.Should().Be(0);
        matrix.UnsupportedResourceCount.Should().Be(0);
        matrix.Issues.Should().BeEmpty();
        matrix.IsConversionFeasible.Should().BeTrue();
    }

    [Fact]
    public void EvaluateCapability_GivenUnsupportedTuningOrScriptResource_EmitsWarningIssueAndUnsupportedStatus()
    {
        // Arrange: ITUN (0x03B33DDF) and S4SCRIPT (0x2800D61B)
        var resources = new List<PackageResourceRow>
        {
            CreateRow(Ts4ResourceTypeIds.CatalogObject, 1),
            CreateRow(0x03B33DDF, 2), // ITUN
            CreateRow(0x2800D61B, 3)  // S4SCRIPT
        };

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "package_with_tuning.package",
            Header: null,
            Resources: resources,
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var matrix = _service.EvaluateCapability(packageResult, GameVersion.Sims3, GameVersion.Sims4);

        // Assert
        matrix.TotalResourcesAnalyzed.Should().Be(3);
        matrix.SupportedResourceCount.Should().Be(1);
        matrix.UnsupportedResourceCount.Should().Be(2);
        matrix.Issues.Should().HaveCount(2);
        matrix.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("ITUN"));
        matrix.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("S4SCRIPT"));
    }

    [Fact]
    public void EvaluateCapability_GivenUnknownResourceTypeId_ClassifiesAsUnsupported_AndEmitsCapa002Issue()
    {
        // Arrange: Unknown TypeId 0x99998888 not in Supported, Whitelisted PassThrough, or Known Unsupported
        var resources = new List<PackageResourceRow>
        {
            CreateRow(Ts4ResourceTypeIds.CatalogObject, 1),
            CreateRow(0x99998888, 2)
        };

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "unknown_type.package",
            Header: null,
            Resources: resources,
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var matrix = _service.EvaluateCapability(packageResult, GameVersion.Sims3, GameVersion.Sims4);

        // Assert
        matrix.TotalResourcesAnalyzed.Should().Be(2);
        matrix.SupportedResourceCount.Should().Be(1);
        matrix.PassThroughResourceCount.Should().Be(0); // Guaranteed NOT passed through
        matrix.UnsupportedResourceCount.Should().Be(1);

        var unknownEntry = matrix.Entries.Single(e => e.TypeId == 0x99998888);
        unknownEntry.CapabilityStatus.Should().Be(ConversionCapabilityStatus.Unsupported);

        matrix.Issues.Should().ContainSingle(i => i.Code == "CAPA002" && i.Message.Contains("0x99998888"));
    }

    [Fact]
    public void EvaluateCapability_GivenWhitelistedNeutralResource_ClassifiesAsPassThrough()
    {
        // Arrange: Whitelisted STBL (0x220557DA) and THUM (0x0D64DFF0)
        var resources = new List<PackageResourceRow>
        {
            CreateRow(Ts4ResourceTypeIds.CatalogObject, 1),
            CreateRow(0x220557DA, 2), // STBL TS3
            CreateRow(0x0D64DFF0, 3)  // THUM
        };

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "neutral_stbl.package",
            Header: null,
            Resources: resources,
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var matrix = _service.EvaluateCapability(packageResult, GameVersion.Sims3, GameVersion.Sims4);

        // Assert
        matrix.TotalResourcesAnalyzed.Should().Be(3);
        matrix.SupportedResourceCount.Should().Be(1);
        matrix.PassThroughResourceCount.Should().Be(2);
        matrix.UnsupportedResourceCount.Should().Be(0);
        matrix.Issues.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateCapability_GivenSims3SpecificTargetTypeIds_ClassifiesAsUnsupported_AndProvidesHumanReadableWarningNames()
    {
        // Arrange: 0x736884F1 (Footprint), 0x03B4C61D (Model RCOL Header), 0x033A1435 (Design Mode Preset)
        var resources = new List<PackageResourceRow>
        {
            CreateRow(Ts4ResourceTypeIds.CatalogObject, 1),
            CreateRow(0x736884F1, 2),
            CreateRow(0x03B4C61D, 3),
            CreateRow(0x033A1435, 4)
        };

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "ts3_specific_types.package",
            Header: null,
            Resources: resources,
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var matrix = _service.EvaluateCapability(packageResult, GameVersion.Sims3, GameVersion.Sims4);

        // Assert
        matrix.TotalResourcesAnalyzed.Should().Be(4);
        matrix.SupportedResourceCount.Should().Be(1);
        matrix.UnsupportedResourceCount.Should().Be(3);
        matrix.Issues.Should().HaveCount(3);

        matrix.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("Footprint (FTPT 0x736884F1)"));
        matrix.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("Model RCOL Header (0x03B4C61D)"));
        matrix.Issues.Should().Contain(i => i.Code == "CAPA001" && i.Message.Contains("Design Mode Preset (0x033A1435)"));

        matrix.Entries.Should().Contain(e => e.TypeId == 0x736884F1 && e.CapabilityStatus == ConversionCapabilityStatus.Unsupported);
        matrix.Entries.Should().Contain(e => e.TypeId == 0x03B4C61D && e.CapabilityStatus == ConversionCapabilityStatus.Unsupported);
        matrix.Entries.Should().Contain(e => e.TypeId == 0x033A1435 && e.CapabilityStatus == ConversionCapabilityStatus.Unsupported);
    }
}
