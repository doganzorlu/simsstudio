using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Package.Services;

public class PackageResourceExporter : IPackageResourceExporter
{
    public async Task<PackageResourceExportResult> ExportAsync(
        PackageResourceExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return PackageResourceExportResult.Failure(string.Empty, string.Empty, "EXPE000", "Export request cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(request.SourcePackagePath))
        {
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath ?? string.Empty,
                request.OutputFilePath ?? string.Empty,
                "EXPE001",
                "Source package file path is null, empty, or whitespace."
            );
        }

        if (!File.Exists(request.SourcePackagePath))
        {
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath,
                request.OutputFilePath ?? string.Empty,
                "EXPE001",
                $"Source package file not found at path '{request.SourcePackagePath}'."
            );
        }

        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath,
                request.OutputFilePath ?? string.Empty,
                "EXPE002",
                "Output file path is null, empty, or whitespace."
            );
        }

        // Canonical path comparison guard: prevent overwriting source package file under any circumstances
        string canonicalSource = Path.GetFullPath(request.SourcePackagePath);
        string canonicalOutput = Path.GetFullPath(request.OutputFilePath);

        StringComparison pathComparison = OperatingSystem.IsLinux()
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (string.Equals(canonicalSource, canonicalOutput, pathComparison))
        {
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath,
                request.OutputFilePath,
                "EXPE008",
                "Target output file path cannot be the same as the source package file path."
            );
        }

        if (File.Exists(request.OutputFilePath) && !request.AllowOverwrite)
        {
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath,
                request.OutputFilePath,
                "EXPE005",
                $"Output file already exists at '{request.OutputFilePath}' and overwrite is disabled."
            );
        }

        if (request.Offset < 0)
        {
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath,
                request.OutputFilePath,
                "EXPE003",
                $"Invalid negative resource offset: {request.Offset}."
            );
        }

        if (request.CompressedSize == 0)
        {
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath,
                request.OutputFilePath,
                "EXPE004",
                "Resource compressed size is 0 bytes."
            );
        }

        string tempFilePath = $"{request.OutputFilePath}.tmp.{Guid.NewGuid():N}";

        try
        {
            await using var sourceStream = new FileStream(
                request.SourcePackagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true
            );

            long fileLength = sourceStream.Length;

            // Overflow-safe bounds check: request.CompressedSize > (fileLength - request.Offset)
            if (request.Offset > fileLength || request.CompressedSize > (fileLength - request.Offset))
            {
                return PackageResourceExportResult.Failure(
                    request.SourcePackagePath,
                    request.OutputFilePath,
                    "EXPE006",
                    $"Requested byte range (offset {request.Offset}, size {request.CompressedSize}) exceeds source file length ({fileLength} bytes)."
                );
            }

            string? targetDirectory = Path.GetDirectoryName(request.OutputFilePath);
            if (!string.IsNullOrWhiteSpace(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            bool writeSuccess = false;
            await using (var tempStream = new FileStream(
                tempFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true
            ))
            {
                sourceStream.Seek(request.Offset, SeekOrigin.Begin);

                byte[] buffer = new byte[Math.Min(8192, request.CompressedSize)];
                long bytesRemaining = request.CompressedSize;

                while (bytesRemaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
                    int bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    await tempStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    bytesRemaining -= bytesRead;
                }

                if (bytesRemaining == 0)
                {
                    writeSuccess = true;
                }
            }

            if (!writeSuccess)
            {
                CleanupTempFile(tempFilePath);
                return PackageResourceExportResult.Failure(
                    request.SourcePackagePath,
                    request.OutputFilePath,
                    "EXPE007",
                    "Premature end of stream while reading resource bytes."
                );
            }

            File.Move(tempFilePath, request.OutputFilePath, overwrite: request.AllowOverwrite);

            return new PackageResourceExportResult(
                true,
                request.SourcePackagePath,
                request.OutputFilePath,
                request.CompressedSize,
                Array.Empty<ConversionIssue>()
            );
        }
        catch (OperationCanceledException)
        {
            CleanupTempFile(tempFilePath);
            throw;
        }
        catch (Exception ex)
        {
            CleanupTempFile(tempFilePath);
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath,
                request.OutputFilePath,
                "EXPE009",
                $"Raw resource export failed with error: {ex.Message}"
            );
        }
    }

    private static void CleanupTempFile(string tempFilePath)
    {
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch
        {
            // Suppress cleanup exceptions
        }
    }
}
