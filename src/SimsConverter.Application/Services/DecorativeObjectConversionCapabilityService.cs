using System;
using System.Collections.Generic;
using System.Linq;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Constants;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Models;

namespace SimsConverter.Application.Services;

public class DecorativeObjectConversionCapabilityService : IDecorativeObjectConversionCapabilityService
{
    private static readonly HashSet<uint> SupportedTypeIds = new()
    {
        Ts4ResourceTypeIds.CatalogObject,      // COBJ 0x319E4F1D
        Ts4ResourceTypeIds.ObjectDefinition,  // OBJD 0xC0DB5AE7
        0x02DC343F,                           // OBJD (Alt TS3)
        Ts4ResourceTypeIds.Model,             // MODL 0x01661233
        Ts4ResourceTypeIds.ModelLod,          // MLOD 0x01D10F34
        Ts4ResourceTypeIds.Geom,              // GEOM 0x015A1849
        0x015A182C,                           // GEOM (RCOL)
        Ts4ResourceTypeIds.MaterialDefinition,// RMAT 0x2172D019
        0x01D0E75D,                           // RMAT (Alt)
        0x0333406C,                           // RMAT (Alt)
        Ts4ResourceTypeIds.Rig,               // RIG  0x8EAF13DE
        Ts4ResourceTypeIds.Slot,              // RSLT 0xD3044521
        0x05B47D14,                           // RSLT (Alt)
        0x00B2D882,                           // DDS / _IMG
        0x3453CF95,                           // TS4 RLE2 Texture
        0x2BC04EDF,                           // TS4 LRLE Texture
        0xDB43D069,                           // DDS (Alt)
        0x2F7D0004                            // Image Resource
    };

    public static readonly HashSet<uint> PassThroughTypeIds = new()
    {
        0x220557DA, // STBL (String Table TS3)
        0x220557DB, // STBL (String Table TS4)
        0x0D64DFF0, // THUM (Thumbnail 1)
        0x0D64DFF1, // THUM (Thumbnail 2)
        0x00669D44  // ICON
    };

    private static readonly Dictionary<uint, string> KnownTypeNames = new()
    {
        { Ts4ResourceTypeIds.CatalogObject, "Catalog Object (COBJ)" },
        { Ts4ResourceTypeIds.ObjectDefinition, "Object Definition (OBJD)" },
        { 0x02DC343F, "Object Definition (OBJD TS3)" },
        { Ts4ResourceTypeIds.Model, "Object Model (MODL)" },
        { Ts4ResourceTypeIds.ModelLod, "Model LOD (MLOD)" },
        { Ts4ResourceTypeIds.Geom, "Geometry Mesh (GEOM TS4/TS3)" },
        { 0x015A182C, "Geometry Mesh (GEOM RCOL)" },
        { Ts4ResourceTypeIds.MaterialDefinition, "Material Definition (RMAT)" },
        { Ts4ResourceTypeIds.Rig, "Skeleton Rig (RIG)" },
        { Ts4ResourceTypeIds.Slot, "Slot Layout (RSLT)" },
        { 0x00B2D882, "Texture Image (_IMG/DDS)" },
        { 0x3453CF95, "TS4 RLE2 Texture" },
        { 0x2BC04EDF, "TS4 LRLE Texture" },
        { 0x220557DA, "String Table (STBL TS3)" },
        { 0x220557DB, "String Table (STBL TS4)" },
        { 0x03B33DDF, "Interaction Tuning (ITUN)" },
        { 0x03B33DDE, "Binary Constants (BCON)" },
        { 0x73878036, "Visual Proxy (VPXY)" },
        { 0x2800D61B, "Python Script (S4SCRIPT)" },
        { Ts4ResourceTypeIds.CasPartTS4, "CAS Part (CASP TS4)" },
        { Ts4ResourceTypeIds.CasPartTS3, "CAS Part (CASP TS3)" },
        { 0x03B33DDD, "Preset Composite (COMP)" },
        { 0x736884F1, "Footprint (FTPT 0x736884F1)" },
        { 0x03B4C61D, "Model RCOL Header (0x03B4C61D)" },
        { 0x033A1435, "Design Mode Preset (0x033A1435)" }
    };

    private static readonly HashSet<uint> KnownUnsupportedTypeIds = new()
    {
        0x03B33DDF, // ITUN
        0x03B33DDE, // BCON
        0x73878036, // VPXY
        0x2800D61B, // S4SCRIPT
        Ts4ResourceTypeIds.CasPartTS4, // CASP TS4
        Ts4ResourceTypeIds.CasPartTS3, // CASP TS3
        0x03B33DDD, // COMP
        0x736884F1, // Footprint (FTPT)
        0x03B4C61D, // Model RCOL Header
        0x033A1435  // Design Mode Preset
    };

    public DecorativeObjectConversionCapabilityMatrix EvaluateCapability(
        PackageInspectionResult packageResult,
        GameVersion sourceVersion,
        GameVersion targetVersion)
    {
        if (packageResult?.Resources == null || packageResult.Resources.Count == 0)
        {
            return DecorativeObjectConversionCapabilityMatrix.Empty(sourceVersion, targetVersion);
        }

        var entries = new List<ConversionCapabilityEntry>();
        var issues = new List<ConversionIssue>();

        var groupedResources = packageResult.Resources.GroupBy(r => r.TypeId);

        int supportedCount = 0;
        int passThroughCount = 0;
        int unsupportedCount = 0;

        string directionLabel = $"{sourceVersion} -> {targetVersion}";

        foreach (var group in groupedResources)
        {
            uint typeId = group.Key;
            int count = group.Count();
            string typeName = KnownTypeNames.TryGetValue(typeId, out var name) ? name : $"Unknown (0x{typeId:X8})";

            ConversionCapabilityStatus status;
            string note;

            if (SupportedTypeIds.Contains(typeId))
            {
                status = ConversionCapabilityStatus.Supported;
                note = $"Supported for bidirectional {directionLabel} conversion.";
                supportedCount += count;
            }
            else if (PassThroughTypeIds.Contains(typeId))
            {
                status = ConversionCapabilityStatus.PassThrough;
                note = $"Pass-through preserved neutral metadata for {directionLabel}.";
                passThroughCount += count;
            }
            else if (KnownUnsupportedTypeIds.Contains(typeId))
            {
                status = ConversionCapabilityStatus.Unsupported;
                note = $"Unsupported game-specific resource for {directionLabel}; will be skipped.";
                unsupportedCount += count;

                issues.Add(new ConversionIssue(
                    Code: "CAPA001",
                    Message: $"Resource '0x{typeId:X8}' ({typeName}) is not supported for {directionLabel} decorative object conversion and will be skipped.",
                    Severity: ConversionIssueSeverity.Warning,
                    TargetVersion: targetVersion
                ));
            }
            else
            {
                // Strict Whitelist Policy: Unrecognized resource types are classified as Unsupported and skipped with CAPA002 warning
                status = ConversionCapabilityStatus.Unsupported;
                note = $"Unrecognized resource type 0x{typeId:X8} is not in the whitelist for {directionLabel}; will be skipped.";
                unsupportedCount += count;

                issues.Add(new ConversionIssue(
                    Code: "CAPA002",
                    Message: $"Resource '0x{typeId:X8}' ({typeName}) is an unrecognized resource type and is not supported for {directionLabel} conversion; it will be skipped.",
                    Severity: ConversionIssueSeverity.Warning,
                    TargetVersion: targetVersion
                ));
            }

            entries.Add(new ConversionCapabilityEntry(
                TypeId: typeId,
                TypeName: typeName,
                CapabilityStatus: status,
                SourceGameVersion: sourceVersion,
                TargetGameVersion: targetVersion,
                ResourceCount: count,
                Note: note
            ));
        }

        return new DecorativeObjectConversionCapabilityMatrix(
            SourceGameVersion: sourceVersion,
            TargetGameVersion: targetVersion,
            TotalResourcesAnalyzed: packageResult.Resources.Count,
            SupportedResourceCount: supportedCount,
            PassThroughResourceCount: passThroughCount,
            UnsupportedResourceCount: unsupportedCount,
            Entries: entries.AsReadOnly(),
            Issues: issues.AsReadOnly(),
            IsConversionFeasible: packageResult.IsSuccess
        );
    }
}
