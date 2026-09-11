using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Mesh.Tests;

public class Ts4MeshResourceClassifierTests
{
    private readonly MeshResourceClassifier _classifier = new();

    [Fact]
    public void Classify_KnownTs4ModlResource_ReturnsKnownMeshAndSims4GameVersion()
    {
        // Arrange: TS4 MODL Object Model (0x01661233)
        var entry = new PackageResourceEntry(
            new PackageResourceId(MeshTypeIds.TsSharedModel, 0x00000000u, 0x123456789ABCDEF0UL),
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            0
        );

        // Act
        var result = _classifier.Classify(entry, GameVersion.Sims4);

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Geometry);
        result.FormatName.Should().Contain("MODL");
        result.DetectedGameVersion.Should().Be(GameVersion.Sims4);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_KnownTs4MlodResource_ReturnsKnownMeshAndSims4GameVersion()
    {
        // Arrange: TS4 MLOD Object Model LOD (0x01D10F34)
        var entry = new PackageResourceEntry(
            new PackageResourceId(MeshTypeIds.TsSharedModelLod, 0x00000000u, 0x123456789ABCDEF0UL),
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            0
        );

        // Act
        var result = _classifier.Classify(entry, GameVersion.Sims4);

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Geometry);
        result.FormatName.Should().Contain("MLOD");
        result.DetectedGameVersion.Should().Be(GameVersion.Sims4);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_Ts3GeomResourceWithTs4GameVersionHint_ReturnsTs4GeomFormatNameAndSims4Version()
    {
        // Arrange: GEOM (0x015A1849) with GameVersion.Sims4 hint
        var entry = new PackageResourceEntry(
            new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000u, 0x123456789ABCDEF0UL),
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            0
        );

        // Act
        var result = _classifier.Classify(entry, GameVersion.Sims4);

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Geometry);
        result.FormatName.Should().Be("TS4 Geometry (GEOM)");
        result.DetectedGameVersion.Should().Be(GameVersion.Sims4);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_Ts3GeomResourceWithoutTs4Hint_PreservesTs3GeomRegressionBehavior()
    {
        // Arrange: GEOM (0x015A1849) without game version hint
        var entry = new PackageResourceEntry(
            new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000u, 0x123456789ABCDEF0UL),
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            0
        );

        // Act
        var result = _classifier.Classify(entry);

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Geometry);
        result.FormatName.Should().Be("TS3 Geometry (GEOM)");
        result.DetectedGameVersion.Should().Be(GameVersion.Sims3);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_UnverifiedTs4CandidateTypeId_ReturnsUnknownClassificationWithMeshWarningIssue()
    {
        // Arrange: Unverified TypeId candidate 0x025C6425
        uint unverifiedCandidateTypeId = 0x025C6425u;
        var entry = new PackageResourceEntry(
            new PackageResourceId(unverifiedCandidateTypeId, 0x00000000u, 0x123456789ABCDEF0UL),
            500,
            1024,
            1024,
            PackageCompressionKind.None,
            0
        );

        // Act
        var result = _classifier.Classify(entry, GameVersion.Sims4);

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be(MeshClassificationKind.Unknown);
        result.RoleKind.Should().Be(MeshRoleKind.Unknown);
        result.FormatName.Should().Be("Unknown Resource");
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("MESHC001");
        result.Issues[0].Severity.Should().Be(ConversionIssueSeverity.Warning);
    }

    [Fact]
    public void MeshTypeIdsConstants_DoNotIncludeUnverifiedCandidateTypeIds()
    {
        // Arrange & Act: Inspect public constants in MeshTypeIds via reflection
        var fields = typeof(MeshTypeIds).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        var constantValues = fields
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(uint))
            .Select(f => (uint)f.GetValue(null)!)
            .ToList();

        // Assert
        constantValues.Should().NotContain(0x025C6425u, "Unverified TypeId 0x025C6425 must not be present as a production constant");
        constantValues.Should().NotContain(0x02864C99u, "Unverified TypeId 0x02864C99 must not be present as a production constant");

        // Verify verified production constants are present
        constantValues.Should().Contain(MeshTypeIds.Ts3Geom);
        constantValues.Should().Contain(MeshTypeIds.TsSharedModel);
        constantValues.Should().Contain(MeshTypeIds.TsSharedModelLod);
        constantValues.Should().Contain(MeshTypeIds.TsSharedRig);
    }
}
