using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Mesh.Tests;

public class MeshResourceClassifierTests
{
    private readonly MeshResourceClassifier _classifier = new();

    [Fact]
    public void Classify_KnownTs3GeomResource_ReturnsKnownMeshWithGeometryRoleAndSims3Version()
    {
        // Arrange: TS3 GEOM (0x015A1849)
        var resId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0x00000000, 0x123456789ABCDEF0UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Geometry);
        result.FormatName.Should().Be("TS3 Geometry (GEOM)");
        result.DetectedGameVersion.Should().Be(GameVersion.Sims3);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_SharedModelResource_WithoutGameVersionHint_PreservesUnknownGameVersion()
    {
        // Arrange: Shared MODL (0x01661233) without hint
        var resId = new PackageResourceId(MeshTypeIds.TsSharedModel, 0x00000000, 0x1111UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, gameVersionHint: GameVersion.Unknown);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Geometry);
        result.FormatName.Should().Be("Model (MODL)");
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown, "Ambiguous shared MODL (0x01661233) MUST NOT guess game version without explicit hint");
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_SharedModelResource_WithGameVersionHint_ReturnsHintedGameVersion()
    {
        // Arrange: Shared MODL (0x01661233) with Sims4 hint
        var resId = new PackageResourceId(MeshTypeIds.TsSharedModel, 0x00000000, 0x1111UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, gameVersionHint: GameVersion.Sims4);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Geometry);
        result.DetectedGameVersion.Should().Be(GameVersion.Sims4);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_SharedModelLodResource_WithoutGameVersionHint_PreservesUnknownGameVersion()
    {
        // Arrange: Shared MLOD (0x01D10F34) without hint
        var resId = new PackageResourceId(MeshTypeIds.TsSharedModelLod, 0x00000000, 0x2222UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, gameVersionHint: GameVersion.Unknown);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Geometry);
        result.FormatName.Should().Be("Model LOD (MLOD)");
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown, "Ambiguous shared MLOD (0x01D10F34) MUST NOT guess game version without explicit hint");
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_SharedModelLodResource_WithGameVersionHint_ReturnsHintedGameVersion()
    {
        // Arrange: Shared MLOD (0x01D10F34) with Sims3 hint
        var resId = new PackageResourceId(MeshTypeIds.TsSharedModelLod, 0x00000000, 0x2222UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, gameVersionHint: GameVersion.Sims3);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Geometry);
        result.DetectedGameVersion.Should().Be(GameVersion.Sims3);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_SharedBlendGeometry_WithoutGameVersionHint_PreservesUnknownGameVersion()
    {
        // Arrange: Shared BGEO (0x067CAA11) without hint
        var resId = new PackageResourceId(MeshTypeIds.TsSharedBlendGeometry, 0x00000000, 0x4444UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, gameVersionHint: GameVersion.Unknown);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Morph);
        result.FormatName.Should().Be("Blend Geometry (BGEO)");
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown, "Ambiguous shared BGEO (0x067CAA11) MUST NOT guess game version without explicit hint");
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_SharedBlendGeometry_WithSims3GameVersionHint_ReturnsSims3Version()
    {
        // Arrange: Shared BGEO (0x067CAA11) with Sims3 hint
        var resId = new PackageResourceId(MeshTypeIds.TsSharedBlendGeometry, 0x00000000, 0x4444UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, gameVersionHint: GameVersion.Sims3);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Morph);
        result.DetectedGameVersion.Should().Be(GameVersion.Sims3);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_SharedBlendGeometry_WithSims4GameVersionHint_ReturnsSims4Version()
    {
        // Arrange: Shared BGEO (0x067CAA11) with Sims4 hint
        var resId = new PackageResourceId(MeshTypeIds.TsSharedBlendGeometry, 0x00000000, 0x5555UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, gameVersionHint: GameVersion.Sims4);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Morph);
        result.DetectedGameVersion.Should().Be(GameVersion.Sims4);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_SharedRigResource_WithoutGameVersionHint_PreservesUnknownGameVersion()
    {
        // Arrange: Shared RIG (0x8EAF13DE) without hint
        var resId = new PackageResourceId(MeshTypeIds.TsSharedRig, 0x00000000, 0x3333UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, gameVersionHint: GameVersion.Unknown);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.KnownMesh);
        result.RoleKind.Should().Be(MeshRoleKind.Rig);
        result.FormatName.Should().Be("Rig / Skeleton");
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_UnknownTypeId_ProducesMESHC001WarningIssueAndUnknownKind()
    {
        // Arrange: Unrecognized TypeId 0x77777777
        var resId = new PackageResourceId(0x77777777u, 0x00000000, 0x9999UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.Unknown);
        result.RoleKind.Should().Be(MeshRoleKind.Unknown);
        result.FormatName.Should().Be("Unknown Resource");
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("MESHC001");
    }

    [Fact]
    public void Classify_NullEntry_ReturnsControlledFailureIssueMESHC000()
    {
        // Act
        var result = _classifier.Classify(null!);

        // Assert
        result.Classification.Should().Be(MeshClassificationKind.Unknown);
        result.RoleKind.Should().Be(MeshRoleKind.Unknown);
        result.FormatName.Should().Be("Null Entry");
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("MESHC000");
    }

    [Fact]
    public void ClassifyBatch_MultipleEntries_PreservesDeterministicInputOrder()
    {
        // Arrange
        var geomId = new PackageResourceId(MeshTypeIds.Ts3Geom, 0, 1);
        var modelId = new PackageResourceId(MeshTypeIds.TsSharedModel, 0, 2);
        var unknownId = new PackageResourceId(0x12345678u, 0, 3);

        var entries = new[]
        {
            new PackageResourceEntry(geomId, 10, 100, 100, PackageCompressionKind.None, 0),
            new PackageResourceEntry(modelId, 20, 200, 200, PackageCompressionKind.None, 0),
            new PackageResourceEntry(unknownId, 30, 300, 300, PackageCompressionKind.None, 0)
        };

        // Act
        var results = _classifier.ClassifyBatch(entries);

        // Assert
        results.Should().HaveCount(3);
        results[0].ResourceId.Should().Be(geomId);
        results[0].Classification.Should().Be(MeshClassificationKind.KnownMesh);

        results[1].ResourceId.Should().Be(modelId);
        results[1].Classification.Should().Be(MeshClassificationKind.KnownMesh);

        results[2].ResourceId.Should().Be(unknownId);
        results[2].Classification.Should().Be(MeshClassificationKind.Unknown);
    }

    [Fact]
    public void MeshTypeIdsCatalogConstants_MatchVerifiedSourceHexValues()
    {
        // Assert: Verified hex constants from S3PE, S4Studio, ModTheSims & TheSims4ModdersReference
        MeshTypeIds.Ts3Geom.Should().Be(0x015A1849u, "TS3 GEOM Model Mesh TypeId");
        MeshTypeIds.TsSharedModel.Should().Be(0x01661233u, "Shared Model (MODL) TypeId [TheSims4ModdersReference & ModTheSims TS3]");
        MeshTypeIds.TsSharedModelLod.Should().Be(0x01D10F34u, "Shared Model LOD (MLOD) TypeId [TheSims4ModdersReference & ModTheSims TS3]");
        MeshTypeIds.TsSharedRig.Should().Be(0x8EAF13DEu, "Shared RIG Skeleton Resource TypeId");
        MeshTypeIds.TsSharedSlot.Should().Be(0xD3044521u, "Shared RSLT Slot Resource TypeId");
        MeshTypeIds.TsSharedBlendGeometry.Should().Be(0x067CAA11u, "Shared BGEO Blend Geometry Resource TypeId [TheSims4ModdersReference & ModTheSims TS3]");
    }
}
