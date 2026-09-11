using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Package.Services;

public class Sims3PackPayloadExporter : ISims3PackPayloadExporter
{
    private static readonly byte[] DbpfMagic = "DBPF"u8.ToArray();

    public async Task<Sims3PackPayloadExportResult> ExportAsync(
        Sims3PackPayloadExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Sims3PackPayloadExportResult.Failure(string.Empty, string.Empty, "S3PE000", "Export request is null.");
        }

        string sourcePath = request.SourceSims3PackPath ?? string.Empty;
        string outputPath = request.OutputFilePath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE001", "Source Sims3Pack file path is invalid or file does not exist.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE002", "Target output file path is invalid or empty.");
        }

        if (request.CatalogEntry == null || request.CatalogEntry.Kind != Sims3PackPayloadKind.DbpfPackage)
        {
            return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE001", "Export requested for non-DBPF payload candidate.");
        }

        // Path comparison: OS-specific case sensitivity check to protect source file
        try
        {
            string canonicalSource = Path.GetFullPath(sourcePath);
            string canonicalOutput = Path.GetFullPath(outputPath);
            StringComparison pathComparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (string.Equals(canonicalSource, canonicalOutput, pathComparison))
            {
                return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE008", "Target output file path cannot be identical to source Sims3Pack file path.");
            }
        }
        catch (Exception ex)
        {
            return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE008", $"Invalid file path: {ex.Message}");
        }

        if (File.Exists(outputPath) && !request.AllowOverwrite)
        {
            return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE005", "Target output file already exists and AllowOverwrite is false.");
        }

        if (request.CatalogEntry.EstimatedSizeBytes.HasValue && request.CatalogEntry.EstimatedSizeBytes.Value <= 0)
        {
            return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE004", "Invalid estimated payload size: <= 0 bytes.");
        }

        string targetDir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? string.Empty;
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            try
            {
                Directory.CreateDirectory(targetDir);
            }
            catch (Exception ex)
            {
                return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE003", $"Failed to create target output directory: {ex.Message}");
            }
        }

        string tempFilePath = Path.Combine(
            string.IsNullOrEmpty(targetDir) ? Path.GetTempPath() : targetDir,
            $"{Path.GetFileName(outputPath)}.tmp.{Guid.NewGuid():N}"
        );

        bool exportSuccess = false;

        try
        {
            long bytesCopied = 0;

            await using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                long fileLength = sourceStream.Length;

                if (request.CatalogEntry.DataOffset < 0 || request.CatalogEntry.DataOffset >= fileLength)
                {
                    CleanupTempFile(tempFilePath);
                    return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE006", $"Invalid payload offset ({request.CatalogEntry.DataOffset}) beyond source stream length ({fileLength}).");
                }

                long bytesToCopy;
                if (request.CatalogEntry.EstimatedSizeBytes.HasValue)
                {
                    bytesToCopy = request.CatalogEntry.EstimatedSizeBytes.Value;
                    if (request.CatalogEntry.DataOffset + bytesToCopy > fileLength)
                    {
                        CleanupTempFile(tempFilePath);
                        return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE006", $"Payload byte range ({request.CatalogEntry.DataOffset} + {bytesToCopy}) extends beyond source file boundary ({fileLength}).");
                    }
                }
                else
                {
                    bytesToCopy = fileLength - request.CatalogEntry.DataOffset;
                }

                sourceStream.Seek(request.CatalogEntry.DataOffset, SeekOrigin.Begin);

                await using (var tempStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    byte[] buffer = new byte[8192];
                    long remaining = bytesToCopy;

                    while (remaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int readCount = (int)Math.Min(buffer.Length, remaining);
                        int read = await sourceStream.ReadAsync(buffer.AsMemory(0, readCount), cancellationToken);

                        if (read == 0)
                        {
                            CleanupTempFile(tempFilePath);
                            return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE006", "Premature end of source stream during payload byte copy.");
                        }

                        await tempStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        bytesCopied += read;
                        remaining -= read;
                    }

                    await tempStream.FlushAsync(cancellationToken);
                }
            }

            // Post-Export Validation: Inspect DBPF header of exported payload file
            await using (var verifyStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 96, useAsync: true))
            {
                byte[] headerBuffer = new byte[96];
                int read = await verifyStream.ReadAsync(headerBuffer.AsMemory(0, 96), cancellationToken);

                List<ConversionIssue>? validationIssues = null;
                bool headerValid = read >= 96 && Sims3PackPayloadCatalogScanner.ValidateDbpfHeader(headerBuffer.AsSpan(0, read), 0, verifyStream.Length, out validationIssues);
                if (!headerValid)
                {
                    CleanupTempFile(tempFilePath);
                    string issueMsg = validationIssues != null && validationIssues.Count > 0 ? validationIssues[0].Message : "Exported payload does not contain valid DBPF magic header.";
                    return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE007", $"Exported payload failed DBPF header validation: {issueMsg}");
                }
            }

            // Atomic move to final output path
            File.Move(tempFilePath, outputPath, overwrite: request.AllowOverwrite);
            exportSuccess = true;

            return new Sims3PackPayloadExportResult(
                IsSuccess: true,
                SourceSims3PackPath: sourcePath,
                OutputFilePath: outputPath,
                ExportedBytes: bytesCopied,
                Issues: Array.Empty<ConversionIssue>()
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
            return Sims3PackPayloadExportResult.Failure(sourcePath, outputPath, "S3PE009", $"Export operation failed: {ex.Message}");
        }
        finally
        {
            if (!exportSuccess)
            {
                CleanupTempFile(tempFilePath);
            }
        }
    }

    private static void CleanupTempFile(string tempFilePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch
        {
            // Ignore temp file deletion cleanup errors
        }
    }
}
