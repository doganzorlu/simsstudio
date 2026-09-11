using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Package.Services;

public class PackageResourcePayloadReader : IPackageResourcePayloadReader
{
    public PackageResourcePayloadResult ReadPayload(string packageFilePath, PackageResourceEntry entry)
    {
        var issues = new List<ConversionIssue>();

        if (string.IsNullOrWhiteSpace(packageFilePath))
        {
            issues.Add(new ConversionIssue("EXPR000", "Package file path is null or whitespace.", ConversionIssueSeverity.Error));
            return new PackageResourcePayloadResult(false, null, issues.AsReadOnly());
        }

        if (entry == null || entry.Id == null)
        {
            issues.Add(new ConversionIssue("EXPR001", "Package resource entry is null.", ConversionIssueSeverity.Error));
            return new PackageResourcePayloadResult(false, null, issues.AsReadOnly());
        }

        if (!File.Exists(packageFilePath))
        {
            issues.Add(new ConversionIssue("EXPR002", $"Package file does not exist: {packageFilePath}", ConversionIssueSeverity.Error));
            return new PackageResourcePayloadResult(false, null, issues.AsReadOnly());
        }

        try
        {
            using var stream = new FileStream(packageFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (entry.DataOffset < 0 || entry.DataOffset + entry.CompressedSize > stream.Length)
            {
                issues.Add(new ConversionIssue(
                    "EXPR003",
                    $"Resource entry payload offset out of bounds: offset {entry.DataOffset} + size {entry.CompressedSize} exceeds file length {stream.Length}.",
                    ConversionIssueSeverity.Error
                ));
                return new PackageResourcePayloadResult(false, null, issues.AsReadOnly());
            }

            stream.Seek(entry.DataOffset, SeekOrigin.Begin);
            var rawBuffer = new byte[entry.CompressedSize];
            int readBytes = 0;

            while (readBytes < rawBuffer.Length)
            {
                int n = stream.Read(rawBuffer, readBytes, rawBuffer.Length - readBytes);
                if (n == 0) break;
                readBytes += n;
            }

            if (readBytes < rawBuffer.Length)
            {
                issues.Add(new ConversionIssue(
                    "EXPR004",
                    $"Truncated payload read: expected {rawBuffer.Length} bytes, actual {readBytes} bytes.",
                    ConversionIssueSeverity.Error
                ));
                return new PackageResourcePayloadResult(false, null, issues.AsReadOnly());
            }

            // Uncompressed passthrough: valid ONLY for PackageCompressionKind.None
            if (entry.CompressionKind == PackageCompressionKind.None && entry.CompressedSize == entry.DecompressedSize)
            {
                return new PackageResourcePayloadResult(true, rawBuffer, issues.AsReadOnly());
            }

            // Explicit RefPack compression check
            if (entry.CompressionKind == PackageCompressionKind.RefPack)
            {
                if (RefpackDecompressor.TryDecompress(rawBuffer, entry.DecompressedSize, out var decompRefpack))
                {
                    return new PackageResourcePayloadResult(true, decompRefpack, issues.AsReadOnly());
                }

                issues.Add(new ConversionIssue(
                    "PKGP004",
                    $"RefPack (0xFB10) decompression failed for entry {entry.Id.FormattedKey}.",
                    ConversionIssueSeverity.Error
                ));
                return new PackageResourcePayloadResult(false, rawBuffer, issues.AsReadOnly());
            }

            if (rawBuffer.Length >= 2 && RefpackDecompressor.IsRefpackHeader(rawBuffer))
            {
                if (RefpackDecompressor.TryDecompress(rawBuffer, entry.DecompressedSize, out var decompRefpack))
                {
                    return new PackageResourcePayloadResult(true, decompRefpack, issues.AsReadOnly());
                }

                issues.Add(new ConversionIssue(
                    "PKGP004",
                    $"RefPack (0xFB10) decompression failed for entry {entry.Id.FormattedKey}.",
                    ConversionIssueSeverity.Error
                ));
                return new PackageResourcePayloadResult(false, rawBuffer, issues.AsReadOnly());
            }

            // Decompression path for Zlib or compressed entries
            if (PackagePayloadDecompressor.TryDecompressZlib(rawBuffer, entry.DecompressedSize, out var decompressedPayload))
            {
                return new PackageResourcePayloadResult(true, decompressedPayload, issues.AsReadOnly());
            }
            else
            {
                issues.Add(new ConversionIssue(
                    "PKGP002",
                    $"Failed to decompress Zlib payload for entry {entry.Id.FormattedKey}. Expected decompressed size: {entry.DecompressedSize} bytes.",
                    ConversionIssueSeverity.Error
                ));
                return new PackageResourcePayloadResult(false, rawBuffer, issues.AsReadOnly());
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ConversionIssue("EXPR005", $"Failed to read resource payload: {ex.Message}", ConversionIssueSeverity.Error));
            return new PackageResourcePayloadResult(false, null, issues.AsReadOnly());
        }
    }

    public async Task<PackageResourcePayloadResult> ReadPayloadAsync(
        string packageFilePath,
        PackageResourceEntry entry,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ReadPayload(packageFilePath, entry), cancellationToken).ConfigureAwait(false);
    }
}
