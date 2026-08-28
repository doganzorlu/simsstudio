using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using SimsConverter.Textures.Constants;
using SimsConverter.Textures.Contracts;

namespace SimsConverter.Textures.Services;

public class TextureResourceExtractor : ITextureResourceExtractor
{
    private readonly IPackageResourceExporter _packageExporter;
    private static readonly Regex InvalidFileNameCharRegex = new(@"[\x00-\x1F\x7F\x22\x3C\x3E\x7C\x3A\x2A\x3F\x5C\x2F]", RegexOptions.Compiled);

    public TextureResourceExtractor(IPackageResourceExporter packageExporter)
    {
        _packageExporter = packageExporter ?? throw new ArgumentNullException(nameof(packageExporter));
    }

    public async Task<TextureResourceExtractResult> ExtractAsync(
        TextureResourceExtractRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.Entry == null || request.Entry.Id == null)
        {
            return TextureResourceExtractResult.Failure(string.Empty, string.Empty, "TEXE000", "Texture extract request or resource entry is null.");
        }

        if (request.Classification == null || request.Classification.Classification != TextureClassificationKind.KnownTexture)
        {
            return TextureResourceExtractResult.Failure(request.SourcePackagePath ?? string.Empty, string.Empty, "TEXE001", "Resource is not a known/classified texture resource.");
        }

        if (request.Classification.ResourceId == null || !request.Classification.ResourceId.Equals(request.Entry.Id))
        {
            return TextureResourceExtractResult.Failure(request.SourcePackagePath ?? string.Empty, string.Empty, "TEXE004", "Classification ResourceId does not match resource entry identity.");
        }

        if (string.IsNullOrWhiteSpace(request.SourcePackagePath) || !File.Exists(request.SourcePackagePath))
        {
            return TextureResourceExtractResult.Failure(request.SourcePackagePath ?? string.Empty, string.Empty, "TEXE002", "Source package file path is invalid or file does not exist.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return TextureResourceExtractResult.Failure(request.SourcePackagePath, string.Empty, "TEXE003", "Output directory is invalid or empty.");
        }

        string extension = GetExtensionForTexture(request.Entry.Id.TypeId);

        string rawBaseName = !string.IsNullOrWhiteSpace(request.CustomFileName)
            ? request.CustomFileName
            : request.Entry.Id.FormattedKey.Replace(':', '_');

        string sanitizedBaseName = InvalidFileNameCharRegex.Replace(rawBaseName, "_").Trim();
        if (string.IsNullOrWhiteSpace(sanitizedBaseName))
        {
            sanitizedBaseName = request.Entry.Id.FormattedKey.Replace(':', '_');
        }

        if (!sanitizedBaseName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            sanitizedBaseName += extension;
        }

        string targetOutputPath = Path.Combine(request.OutputDirectory, sanitizedBaseName);

        // Canonical path separation guard
        string canonicalSource = Path.GetFullPath(request.SourcePackagePath);
        string canonicalOutput = Path.GetFullPath(targetOutputPath);
        if (string.Equals(canonicalSource, canonicalOutput, StringComparison.OrdinalIgnoreCase))
        {
            return TextureResourceExtractResult.Failure(request.SourcePackagePath, targetOutputPath, "TEXE008", "Target output file path cannot be identical to source package file path.");
        }

        var exportRequest = new PackageResourceExportRequest(
            SourcePackagePath: request.SourcePackagePath,
            Offset: request.Entry.DataOffset,
            CompressedSize: request.Entry.CompressedSize,
            OutputFilePath: targetOutputPath,
            AllowOverwrite: request.AllowOverwrite
        );

        var exportResult = await _packageExporter.ExportAsync(exportRequest, cancellationToken);

        return new TextureResourceExtractResult(
            IsSuccess: exportResult.IsSuccess,
            SourcePackagePath: exportResult.SourcePackagePath,
            OutputFilePath: exportResult.OutputFilePath,
            ExportedBytes: exportResult.ExportedBytes,
            Issues: exportResult.Issues
        );
    }

    public static string GetExtensionForTexture(uint typeId)
    {
        return typeId switch
        {
            TextureTypeIds.Ts3DdsTexture => ".dds",
            TextureTypeIds.Ts4Rle2Texture => ".rle2",
            TextureTypeIds.Ts4LrleTexture => ".lrle",
            TextureTypeIds.Ts4PngImage or TextureTypeIds.Ts3SnapshotThumbnail or TextureTypeIds.Ts4CasPartThumbnail => ".png",
            _ => ".raw"
        };
    }
}
