using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Constants;
using SimsConverter.Textures.Contracts;
using SimsConverter.Textures.Services;
using Xunit;
using FluentAssertions;

namespace SimsConverter.Application.Tests;

public class TextureInspectionServiceTests
{
    private readonly StubPackageInspectionService _packageInspectionService = new();
    private readonly ITextureResourceClassifier _textureClassifier = new TextureResourceClassifier();
    private readonly TextureInspectionService _service;

    public TextureInspectionServiceTests()
    {
        _service = new TextureInspectionService(_packageInspectionService, _textureClassifier);
    }

    [Fact]
    public void InspectPackageTextures_KnownDdsResource_SetsCanParseDdsHeaderAndCanExtractRawPayloadTrue()
    {
        // Arrange: TS3 DDS Texture
        var ddsResId = new PackageResourceId(TextureTypeIds.Ts3DdsTexture, 0, 0x123456789ABCDEF0UL);
        var row = new PackageResourceRow(
            TypeId: ddsResId.TypeId,
            GroupId: ddsResId.GroupId,
            InstanceId: ddsResId.InstanceId,
            TypeHex: "0x00B2D882",
            GroupHex: "0x00000000",
            InstanceHex: "0x123456789ABCDEF0",
            FormattedKey: ddsResId.FormattedKey,
            Offset: 100,
            CompressedSize: 500,
            DecompressedSize: 500,
            CompressionKind: PackageCompressionKind.None,
            CompressionName: "None"
        );

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "/test/path.package",
            Header: null,
            Resources: new[] { row },
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var result = _service.InspectPackageTextures(packageResult, GameVersion.Sims3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Rows.Should().ContainSingle();
        var texRow = result.Rows[0];
        texRow.CanExtractRawPayload.Should().BeTrue();
        texRow.CanParseDdsHeader.Should().BeTrue();
        texRow.FormatName.Should().Be("DDS Image Texture");
        texRow.MapKind.Should().Be(TextureMapKind.Diffuse);
        texRow.DetectedGameVersion.Should().Be(GameVersion.Sims3);
    }

    [Fact]
    public void InspectPackageTextures_Ts4Rle2Resource_SetsCanParseDdsHeaderFalseAndCanExtractRawPayloadTrue()
    {
        // Arrange: TS4 RLE2 Texture (0x3453CF95)
        var rle2ResId = new PackageResourceId(TextureTypeIds.Ts4Rle2Texture, 0, 0x1111);
        var row = new PackageResourceRow(
            TypeId: rle2ResId.TypeId,
            GroupId: rle2ResId.GroupId,
            InstanceId: rle2ResId.InstanceId,
            TypeHex: "0x3453CF95",
            GroupHex: "0x00000000",
            InstanceHex: "0x0000000000001111",
            FormattedKey: rle2ResId.FormattedKey,
            Offset: 100,
            CompressedSize: 500,
            DecompressedSize: 500,
            CompressionKind: PackageCompressionKind.None,
            CompressionName: "None"
        );

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "/test/path.package",
            Header: null,
            Resources: new[] { row },
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var result = _service.InspectPackageTextures(packageResult);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Rows.Should().ContainSingle();
        var texRow = result.Rows[0];
        texRow.CanExtractRawPayload.Should().BeTrue("Known RLE2 texture can be exported as raw payload");
        texRow.CanParseDdsHeader.Should().BeFalse("RLE2 texture is not a DDS format and cannot be parsed by DDS header parser");
        texRow.FormatName.Should().Be("TS4 RLE2 Texture");
        texRow.DetectedGameVersion.Should().Be(GameVersion.Sims4);
    }

    [Fact]
    public void InspectPackageTextures_UnknownResource_PreservesClassificationIssueAndIncludesRow()
    {
        // Arrange: Unrecognized TypeId 0x99999999
        var unknownResId = new PackageResourceId(0x99999999u, 0, 0x9999);
        var row = new PackageResourceRow(
            TypeId: unknownResId.TypeId,
            GroupId: unknownResId.GroupId,
            InstanceId: unknownResId.InstanceId,
            TypeHex: "0x99999999",
            GroupHex: "0x00000000",
            InstanceHex: "0x0000000000009999",
            FormattedKey: unknownResId.FormattedKey,
            Offset: 100,
            CompressedSize: 500,
            DecompressedSize: 500,
            CompressionKind: PackageCompressionKind.None,
            CompressionName: "None"
        );

        var packageResult = new PackageInspectionResult(
            IsSuccess: true,
            FilePath: "/test/path.package",
            Header: null,
            Resources: new[] { row },
            Issues: Array.Empty<ConversionIssue>()
        );

        // Act
        var result = _service.InspectPackageTextures(packageResult);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Rows.Should().ContainSingle("Unknown resources MUST NOT be silently omitted");
        var texRow = result.Rows[0];
        texRow.ClassificationKind.Should().Be(TextureClassificationKind.Unknown);
        texRow.CanExtractRawPayload.Should().BeFalse();
        texRow.CanParseDdsHeader.Should().BeFalse();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("TEXC001");
    }

    [Fact]
    public async Task InspectPackageTexturesAsync_PackageInspectionFailure_AggregatesPackageIssuesInResult()
    {
        // Arrange
        _packageInspectionService.ResultToReturn = new PackageInspectionResult(
            IsSuccess: false,
            FilePath: "/invalid/path.package",
            Header: null,
            Resources: Array.Empty<PackageResourceRow>(),
            Issues: new[] { new ConversionIssue("PKGI001", "File not found", ConversionIssueSeverity.Error) }
        );

        var request = new TextureInspectionRequest("/invalid/path.package");

        // Act
        var result = await _service.InspectPackageTexturesAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Rows.Should().BeEmpty();
        result.Issues.Should().ContainSingle();
        result.Issues[0].Code.Should().Be("PKGI001");
    }

    private sealed class StubPackageInspectionService : IPackageInspectionService
    {
        public PackageInspectionResult ResultToReturn { get; set; } = new(false, "", null, Array.Empty<PackageResourceRow>(), Array.Empty<ConversionIssue>());

        public Task<PackageInspectionResult> InspectFileAsync(string packageFilePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }
}
