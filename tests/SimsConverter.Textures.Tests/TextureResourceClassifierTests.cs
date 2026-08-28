using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Constants;
using SimsConverter.Textures.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Textures.Tests;

public class TextureResourceClassifierTests
{
    private readonly TextureResourceClassifier _classifier = new();

    [Fact]
    public void Classify_KnownTs3TextureType_ReturnsKnownTextureClassification()
    {
        // Arrange
        var resId = new PackageResourceId(TextureTypeIds.Ts3DdsTexture, 0, 0x123456789ABCDEF0UL);
        var entry = new PackageResourceEntry(resId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, GameVersion.Sims3);

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be(TextureClassificationKind.KnownTexture);
        result.FormatName.Should().Be("DDS Image Texture");
        result.DetectedGameVersion.Should().Be(GameVersion.Sims3);
        result.MapKind.Should().Be(TextureMapKind.Diffuse);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Classify_KnownTs4TextureTypes_ReturnKnownTextureClassifications()
    {
        // Arrange: TS4 RLE2 Texture (0x3453CF95)
        var rle2Id = new PackageResourceId(TextureTypeIds.Ts4Rle2Texture, 0, 0x1111);
        var rle2Entry = new PackageResourceEntry(rle2Id, 100, 500, 500, PackageCompressionKind.None, 0);

        // Arrange: TS4 LRLE Texture (0x2BC04EDF)
        var lrleId = new PackageResourceId(TextureTypeIds.Ts4LrleTexture, 0, 0x2222);
        var lrleEntry = new PackageResourceEntry(lrleId, 200, 600, 600, PackageCompressionKind.None, 0);

        // Arrange: TS4 PNG Image (0x2F7D0004)
        var pngImageId = new PackageResourceId(TextureTypeIds.Ts4PngImage, 0, 0x3333);
        var pngImageEntry = new PackageResourceEntry(pngImageId, 300, 700, 700, PackageCompressionKind.None, 0);

        // Arrange: TS4 CAS Part Thumbnail (0x3C1AF1F2)
        var casThumbId = new PackageResourceId(TextureTypeIds.Ts4CasPartThumbnail, 0, 0x4444);
        var casThumbEntry = new PackageResourceEntry(casThumbId, 400, 800, 800, PackageCompressionKind.None, 0);

        // Act
        var rle2Result = _classifier.Classify(rle2Entry);
        var lrleResult = _classifier.Classify(lrleEntry);
        var pngImageResult = _classifier.Classify(pngImageEntry);
        var casThumbResult = _classifier.Classify(casThumbEntry);

        // Assert
        rle2Result.Classification.Should().Be(TextureClassificationKind.KnownTexture);
        rle2Result.FormatName.Should().Be("TS4 RLE2 Texture");
        rle2Result.DetectedGameVersion.Should().Be(GameVersion.Sims4);
        TextureTypeIds.Ts4Rle2Texture.Should().Be(0x3453CF95u);

        lrleResult.Classification.Should().Be(TextureClassificationKind.KnownTexture);
        lrleResult.FormatName.Should().Be("TS4 LRLE Texture");
        lrleResult.DetectedGameVersion.Should().Be(GameVersion.Sims4);
        TextureTypeIds.Ts4LrleTexture.Should().Be(0x2BC04EDFu);

        pngImageResult.Classification.Should().Be(TextureClassificationKind.KnownTexture);
        pngImageResult.FormatName.Should().Be("TS4 PNG Image");
        pngImageResult.DetectedGameVersion.Should().Be(GameVersion.Sims4);
        TextureTypeIds.Ts4PngImage.Should().Be(0x2F7D0004u);

        casThumbResult.Classification.Should().Be(TextureClassificationKind.KnownTexture);
        casThumbResult.FormatName.Should().Be("TS4 CAS Part Thumbnail");
        casThumbResult.MapKind.Should().Be(TextureMapKind.Thumbnail);
        TextureTypeIds.Ts4CasPartThumbnail.Should().Be(0x3C1AF1F2u);
    }

    [Fact]
    public void Classify_UnknownResourceTypeId_ProducesDiagnosticIssue()
    {
        // Arrange
        var unknownId = new PackageResourceId(0x99999999u, 0, 0x9999);
        var entry = new PackageResourceEntry(unknownId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, GameVersion.Sims3);

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be(TextureClassificationKind.Unknown);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXC001");
        result.Issues[0].Message.Should().Contain("0x99999999");
    }

    [Fact]
    public void Classify_UnknownGameVersion_DoesNotGuessGameVersionForAmbiguousTypeId()
    {
        // Arrange: Shared DDS TypeId 0x00B2D882 with GameVersion.Unknown
        var ddsId = new PackageResourceId(TextureTypeIds.Ts3DdsTexture, 0, 0xABC);
        var entry = new PackageResourceEntry(ddsId, 100, 500, 500, PackageCompressionKind.None, 0);

        // Act
        var result = _classifier.Classify(entry, GameVersion.Unknown);

        // Assert
        result.Classification.Should().Be(TextureClassificationKind.KnownTexture);
        result.DetectedGameVersion.Should().Be(GameVersion.Unknown, "Classifier MUST NOT guess game version for shared/ambiguous DDS type ID");
    }

    [Fact]
    public void Classify_NullEntry_ReturnsSafeFailureClassification()
    {
        // Act
        var result = _classifier.Classify(null!);

        // Assert
        result.Should().NotBeNull();
        result.Classification.Should().Be(TextureClassificationKind.Unknown);
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXC000");
    }

    [Fact]
    public void ClassifyBatch_GivenListOfEntries_ReturnsClassificationsForArrayList()
    {
        // Arrange
        var entries = new[]
        {
            new PackageResourceEntry(new PackageResourceId(TextureTypeIds.Ts4Rle2Texture, 0, 1), 0, 100, 100, PackageCompressionKind.None, 0),
            new PackageResourceEntry(new PackageResourceId(0xFFFFFFFFu, 0, 2), 0, 100, 100, PackageCompressionKind.None, 0)
        };

        // Act
        var results = _classifier.ClassifyBatch(entries);

        // Assert
        results.Should().HaveCount(2);
        results[0].Classification.Should().Be(TextureClassificationKind.KnownTexture);
        results[1].Classification.Should().Be(TextureClassificationKind.Unknown);
    }
}
