using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Contracts;

namespace SimsConverter.Mesh.Services;

public class MeshResourceClassifier : IMeshResourceClassifier
{
    public MeshResourceClassification Classify(PackageResourceEntry entry, GameVersion gameVersionHint = GameVersion.Unknown)
    {
        if (entry == null || entry.Id == null)
        {
            var nullIssue = new ConversionIssue(
                "MESHC000",
                "PackageResourceEntry or ResourceId is null.",
                ConversionIssueSeverity.Error
            );

            return new MeshResourceClassification(
                ResourceId: null!,
                Classification: MeshClassificationKind.Unknown,
                RoleKind: MeshRoleKind.Unknown,
                FormatName: "Null Entry",
                DetectedGameVersion: GameVersion.Unknown,
                Issues: new[] { nullIssue }
            );
        }

        uint typeId = entry.Id.TypeId;

        switch (typeId)
        {
            case MeshTypeIds.Ts3Geom:
                bool isTs4Geom = (gameVersionHint == GameVersion.Sims4);
                return new MeshResourceClassification(
                    ResourceId: entry.Id,
                    Classification: MeshClassificationKind.KnownMesh,
                    RoleKind: MeshRoleKind.Geometry,
                    FormatName: isTs4Geom ? "TS4 Geometry (GEOM)" : "TS3 Geometry (GEOM)",
                    DetectedGameVersion: isTs4Geom ? GameVersion.Sims4 : GameVersion.Sims3,
                    Issues: Array.Empty<ConversionIssue>()
                );

            case MeshTypeIds.TsSharedModel:
                bool isTs4Model = (gameVersionHint == GameVersion.Sims4);
                return new MeshResourceClassification(
                    ResourceId: entry.Id,
                    Classification: MeshClassificationKind.KnownMesh,
                    RoleKind: MeshRoleKind.Geometry,
                    FormatName: isTs4Model ? "TS4 Object Model (MODL)" : "Model (MODL)",
                    DetectedGameVersion: gameVersionHint,
                    Issues: Array.Empty<ConversionIssue>()
                );

            case MeshTypeIds.TsSharedModelLod:
                bool isTs4Mlod = (gameVersionHint == GameVersion.Sims4);
                return new MeshResourceClassification(
                    ResourceId: entry.Id,
                    Classification: MeshClassificationKind.KnownMesh,
                    RoleKind: MeshRoleKind.Geometry,
                    FormatName: isTs4Mlod ? "TS4 Object Model LOD (MLOD)" : "Model LOD (MLOD)",
                    DetectedGameVersion: gameVersionHint,
                    Issues: Array.Empty<ConversionIssue>()
                );

            case MeshTypeIds.TsSharedRig:
                bool isTs4Rig = (gameVersionHint == GameVersion.Sims4);
                return new MeshResourceClassification(
                    ResourceId: entry.Id,
                    Classification: MeshClassificationKind.KnownMesh,
                    RoleKind: MeshRoleKind.Rig,
                    FormatName: isTs4Rig ? "TS4 Rig / Skeleton" : "Rig / Skeleton",
                    DetectedGameVersion: gameVersionHint,
                    Issues: Array.Empty<ConversionIssue>()
                );

            case MeshTypeIds.TsSharedSlot:
                return new MeshResourceClassification(
                    ResourceId: entry.Id,
                    Classification: MeshClassificationKind.KnownMesh,
                    RoleKind: MeshRoleKind.Slot,
                    FormatName: "Slot Layout (RSLT)",
                    DetectedGameVersion: gameVersionHint,
                    Issues: Array.Empty<ConversionIssue>()
                );

            case MeshTypeIds.TsSharedBlendGeometry:
                return new MeshResourceClassification(
                    ResourceId: entry.Id,
                    Classification: MeshClassificationKind.KnownMesh,
                    RoleKind: MeshRoleKind.Morph,
                    FormatName: "Blend Geometry (BGEO)",
                    DetectedGameVersion: gameVersionHint,
                    Issues: Array.Empty<ConversionIssue>()
                );

            default:
                var warningIssue = new ConversionIssue(
                    "MESHC001",
                    $"Resource TypeId 0x{typeId:X8} is not a recognized mesh or geometry resource.",
                    ConversionIssueSeverity.Warning
                );

                return new MeshResourceClassification(
                    ResourceId: entry.Id,
                    Classification: MeshClassificationKind.Unknown,
                    RoleKind: MeshRoleKind.Unknown,
                    FormatName: "Unknown Resource",
                    DetectedGameVersion: GameVersion.Unknown,
                    Issues: new[] { warningIssue }
                );
        }
    }

    public IReadOnlyList<MeshResourceClassification> ClassifyBatch(
        IEnumerable<PackageResourceEntry> entries,
        GameVersion gameVersionHint = GameVersion.Unknown)
    {
        if (entries == null)
        {
            return Array.Empty<MeshResourceClassification>();
        }

        var results = new List<MeshResourceClassification>();
        foreach (var entry in entries)
        {
            results.Add(Classify(entry, gameVersionHint));
        }

        return results.AsReadOnly();
    }
}
