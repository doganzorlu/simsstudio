using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Constants;
using SimsConverter.Textures.Contracts;

namespace SimsConverter.Application.Services;

public class TextureInspectionService : ITextureInspectionService
{
    private readonly IPackageInspectionService _packageInspectionService;
    private readonly ITextureResourceClassifier _textureClassifier;

    public TextureInspectionService(
        IPackageInspectionService packageInspectionService,
        ITextureResourceClassifier textureClassifier)
    {
        _packageInspectionService = packageInspectionService ?? throw new ArgumentNullException(nameof(packageInspectionService));
        _textureClassifier = textureClassifier ?? throw new ArgumentNullException(nameof(textureClassifier));
    }

    public async Task<TextureInspectionResult> InspectPackageTexturesAsync(
        TextureInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.PackageFilePath))
        {
            return TextureInspectionResult.Failure(
                request?.PackageFilePath ?? string.Empty,
                "TEXA000",
                "Texture inspection request or package file path is null or empty."
            );
        }

        var packageResult = await _packageInspectionService.InspectFileAsync(request.PackageFilePath, cancellationToken).ConfigureAwait(false);
        if (!packageResult.IsSuccess)
        {
            return new TextureInspectionResult(
                IsSuccess: false,
                PackageFilePath: request.PackageFilePath,
                Rows: Array.Empty<TextureResourceRow>(),
                Issues: packageResult.Issues
            );
        }

        return InspectPackageTextures(packageResult, request.GameVersionHint);
    }

    public TextureInspectionResult InspectPackageTextures(
        PackageInspectionResult packageInspection,
        GameVersion gameVersionHint = GameVersion.Unknown)
    {
        if (packageInspection == null)
        {
            return TextureInspectionResult.Failure(string.Empty, "TEXA000", "Package inspection result is null.");
        }

        var aggregatedIssues = new List<ConversionIssue>(packageInspection.Issues);
        var rows = new List<TextureResourceRow>();

        if (packageInspection.Resources != null)
        {
            foreach (var row in packageInspection.Resources)
            {
                var entry = new PackageResourceEntry(
                    new PackageResourceId(row.TypeId, row.GroupId, row.InstanceId),
                    row.Offset,
                    row.CompressedSize,
                    row.DecompressedSize,
                    row.CompressionKind,
                    0
                );

                var classification = _textureClassifier.Classify(entry, gameVersionHint);

                bool canExtractRawPayload = (classification.Classification == TextureClassificationKind.KnownTexture);
                bool canParseDdsHeader = (classification.Classification == TextureClassificationKind.KnownTexture && entry.Id.TypeId == TextureTypeIds.Ts3DdsTexture);

                var textureRow = new TextureResourceRow(
                    FormattedKey: entry.Id.FormattedKey,
                    TypeHex: row.TypeHex,
                    GroupHex: row.GroupHex,
                    InstanceHex: row.InstanceHex,
                    FormatName: classification.FormatName,
                    MapKind: classification.MapKind,
                    DetectedGameVersion: classification.DetectedGameVersion,
                    ClassificationKind: classification.Classification,
                    CanExtractRawPayload: canExtractRawPayload,
                    CanParseDdsHeader: canParseDdsHeader,
                    Issues: classification.Issues,
                    Entry: entry,
                    Classification: classification
                );

                if (classification.Issues != null && classification.Issues.Count > 0)
                {
                    aggregatedIssues.AddRange(classification.Issues);
                }

                rows.Add(textureRow);
            }
        }

        return new TextureInspectionResult(
            IsSuccess: packageInspection.IsSuccess,
            PackageFilePath: packageInspection.FilePath,
            Rows: rows.AsReadOnly(),
            Issues: aggregatedIssues.AsReadOnly()
        );
    }
}
