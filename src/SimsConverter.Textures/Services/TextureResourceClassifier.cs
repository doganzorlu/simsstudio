using System;
using System.Collections.Generic;
using System.Linq;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Constants;
using SimsConverter.Textures.Contracts;

namespace SimsConverter.Textures.Services;

public class TextureResourceClassifier : ITextureResourceClassifier
{
    public TextureResourceClassification Classify(
        PackageResourceEntry entry,
        GameVersion gameVersionHint = GameVersion.Unknown)
    {
        if (entry == null || entry.Id == null)
        {
            var fallbackId = new PackageResourceId(0, 0, 0);
            return TextureResourceClassification.UnknownResource(fallbackId, "TEXC000", "Package resource entry or ResourceId is null.");
        }

        uint typeId = entry.Id.TypeId;

        switch (typeId)
        {
            case TextureTypeIds.Ts4Rle2Texture:
                return new TextureResourceClassification(
                    entry.Id,
                    TextureClassificationKind.KnownTexture,
                    TextureMapKind.Diffuse,
                    "TS4 RLE2 Texture",
                    GameVersion.Sims4,
                    Array.Empty<ConversionIssue>()
                );

            case TextureTypeIds.Ts4LrleTexture:
                return new TextureResourceClassification(
                    entry.Id,
                    TextureClassificationKind.KnownTexture,
                    TextureMapKind.Diffuse,
                    "TS4 LRLE Texture",
                    GameVersion.Sims4,
                    Array.Empty<ConversionIssue>()
                );

            case TextureTypeIds.Ts3DdsTexture:
                return new TextureResourceClassification(
                    entry.Id,
                    TextureClassificationKind.KnownTexture,
                    TextureMapKind.Diffuse,
                    "DDS Image Texture",
                    gameVersionHint != GameVersion.Unknown ? gameVersionHint : GameVersion.Unknown,
                    Array.Empty<ConversionIssue>()
                );

            case TextureTypeIds.Ts4PngImage:
                return new TextureResourceClassification(
                    entry.Id,
                    TextureClassificationKind.KnownTexture,
                    TextureMapKind.Diffuse,
                    "TS4 PNG Image",
                    GameVersion.Sims4,
                    Array.Empty<ConversionIssue>()
                );

            case TextureTypeIds.Ts3SnapshotThumbnail:
                return new TextureResourceClassification(
                    entry.Id,
                    TextureClassificationKind.KnownTexture,
                    TextureMapKind.Thumbnail,
                    "TS3 Snapshot Image",
                    GameVersion.Sims3,
                    Array.Empty<ConversionIssue>()
                );

            case TextureTypeIds.Ts4CasPartThumbnail:
                return new TextureResourceClassification(
                    entry.Id,
                    TextureClassificationKind.KnownTexture,
                    TextureMapKind.Thumbnail,
                    "TS4 CAS Part Thumbnail",
                    GameVersion.Sims4,
                    Array.Empty<ConversionIssue>()
                );

            default:
                return TextureResourceClassification.UnknownResource(
                    entry.Id,
                    "TEXC001",
                    $"Resource TypeId 0x{typeId:X8} is not a recognized texture type."
                );
        }
    }

    public IReadOnlyList<TextureResourceClassification> ClassifyBatch(
        IEnumerable<PackageResourceEntry> entries,
        GameVersion gameVersionHint = GameVersion.Unknown)
    {
        if (entries == null)
        {
            return Array.Empty<TextureResourceClassification>();
        }

        var results = new List<TextureResourceClassification>();
        foreach (var entry in entries)
        {
            if (entry != null)
            {
                results.Add(Classify(entry, gameVersionHint));
            }
        }

        return results.AsReadOnly();
    }
}
