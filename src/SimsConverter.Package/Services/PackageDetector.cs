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

public class PackageDetector : IPackageDetector
{
    private static readonly byte[] DbpfMagic = "DBPF"u8.ToArray();
    private const int StandardHeaderBufferSize = 96;
    private readonly ISims3PackDetector _sims3PackDetector;

    public PackageDetector(ISims3PackDetector? sims3PackDetector = null)
    {
        _sims3PackDetector = sims3PackDetector ?? new Sims3PackDetector();
    }

    public PackageDetectionResult Detect(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 4)
        {
            return PackageDetectionResult.Unknown(
                "PKG001",
                "Buffer length is less than 4 bytes; insufficient length for DBPF magic header inspection."
            );
        }

        bool isDbpfMagic = buffer[..4].SequenceEqual(DbpfMagic);
        if (!isDbpfMagic)
        {
            return PackageDetectionResult.Unknown(
                "PKG002",
                "Header magic sequence does not match DBPF container magic."
            );
        }

        var issues = new List<ConversionIssue>();

        string majorVersion = "0";
        string minorVersion = "0";
        int indexEntryCount = 0;

        if (buffer.Length >= 8)
        {
            int major = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4, 4));
            majorVersion = major.ToString();
        }

        if (buffer.Length >= 12)
        {
            int minor = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(8, 4));
            minorVersion = minor.ToString();
        }

        if (buffer.Length >= 28)
        {
            indexEntryCount = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(24, 4));
            if (indexEntryCount < 0)
            {
                indexEntryCount = 0;
            }
        }

        if (buffer.Length < StandardHeaderBufferSize)
        {
            issues.Add(new ConversionIssue(
                "PKG003",
                $"Truncated DBPF package header. Expected at least {StandardHeaderBufferSize} bytes, received {buffer.Length} bytes.",
                ConversionIssueSeverity.Warning
            ));
        }

        return new PackageDetectionResult(
            PackageContainerKind.Dbpf,
            GameVersion.Unknown,
            PackageDetectionConfidence.Low,
            majorVersion,
            minorVersion,
            indexEntryCount,
            issues.AsReadOnly()
        );
    }

    public async Task<PackageDetectionResult> DetectAsync(Stream? stream, CancellationToken cancellationToken = default)
    {
        if (stream == null || !stream.CanRead)
        {
            return PackageDetectionResult.Unknown(
                "PKG004",
                "Target stream is null or unreadable."
            );
        }

        try
        {
            byte[] headerBuffer = new byte[StandardHeaderBufferSize];
            int totalBytesRead = 0;

            while (totalBytesRead < StandardHeaderBufferSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int bytesRead = await stream.ReadAsync(
                    headerBuffer.AsMemory(totalBytesRead, StandardHeaderBufferSize - totalBytesRead),
                    cancellationToken
                );

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }

            return Detect(headerBuffer.AsSpan(0, totalBytesRead));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PackageDetectionResult.Unknown(
                "PKG005",
                $"Stream inspection failed with error: {ex.Message}"
            );
        }
    }

    public async Task<PackageDetectionResult> DetectFileAsync(string? filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return PackageDetectionResult.Unknown(
                "PKG006",
                "Specified file path is null, empty, or whitespace."
            );
        }

        if (!File.Exists(filePath))
        {
            return PackageDetectionResult.Unknown(
                "PKG007",
                $"File not found at path '{filePath}'."
            );
        }

        if (filePath.EndsWith(".sims3pack", StringComparison.OrdinalIgnoreCase))
        {
            return await _sims3PackDetector.DetectFileAsync(filePath, cancellationToken);
        }

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true
            );

            return await DetectAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PackageDetectionResult.Unknown(
                "PKG008",
                $"Failed to read package file '{filePath}': {ex.Message}"
            );
        }
    }
}
