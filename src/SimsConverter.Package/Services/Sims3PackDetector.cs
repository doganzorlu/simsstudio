using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Package.Services;

public class Sims3PackDetector : ISims3PackDetector
{
    public async Task<PackageDetectionResult> DetectFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            var issue = new ConversionIssue("DET001", "File path is null, empty, or file does not exist.", ConversionIssueSeverity.Error);
            return new PackageDetectionResult(
                PackageContainerKind.Unknown,
                GameVersion.Unknown,
                PackageDetectionConfidence.None,
                "0", "0", 0,
                new[] { issue }
            );
        }

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return await DetectAsync(stream, Path.GetFileName(filePath), cancellationToken);
        }
        catch (Exception ex)
        {
            var issue = new ConversionIssue("DET002", $"Failed to access file for Sims3Pack detection: {ex.Message}", ConversionIssueSeverity.Error);
            return new PackageDetectionResult(
                PackageContainerKind.Unknown,
                GameVersion.Unknown,
                PackageDetectionConfidence.None,
                "0", "0", 0,
                new[] { issue }
            );
        }
    }

    public async Task<PackageDetectionResult> DetectAsync(
        Stream stream,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (stream == null || !stream.CanRead)
        {
            var issue = new ConversionIssue("DET003", "Stream is null or unreadable.", ConversionIssueSeverity.Error);
            return new PackageDetectionResult(
                PackageContainerKind.Unknown,
                GameVersion.Unknown,
                PackageDetectionConfidence.None,
                "0", "0", 0,
                new[] { issue }
            );
        }

        bool isSims3PackExtension = !string.IsNullOrWhiteSpace(fileName) &&
                                    fileName.EndsWith(".sims3pack", StringComparison.OrdinalIgnoreCase);

        byte[] buffer = new byte[1024];
        int bytesRead = 0;

        long originalPosition = 0;
        bool canSeek = stream.CanSeek;

        try
        {
            if (canSeek)
            {
                originalPosition = stream.Position;
                stream.Seek(0, SeekOrigin.Begin);
            }

            // Safely read up to buffer.Length without accessing stream.Length on non-seekable streams
            while (bytesRead < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(bytesRead, buffer.Length - bytesRead), cancellationToken);
                if (read == 0)
                {
                    break;
                }
                bytesRead += read;
            }

            if (bytesRead < 4)
            {
                var issue = new ConversionIssue(
                    "DET004",
                    isSims3PackExtension
                        ? "File has .sims3pack extension but header buffer is under 4 bytes."
                        : "Stream buffer length is under 4 bytes.",
                    ConversionIssueSeverity.Warning
                );

                return new PackageDetectionResult(
                    isSims3PackExtension ? PackageContainerKind.Sims3Pack : PackageContainerKind.Unknown,
                    isSims3PackExtension ? GameVersion.Sims3 : GameVersion.Unknown,
                    PackageDetectionConfidence.None,
                    "0", "0", 0,
                    new[] { issue }
                );
            }

            // Check if file is masquerading as DBPF package
            if (bytesRead >= 4 &&
                buffer[0] == 'D' &&
                buffer[1] == 'B' &&
                buffer[2] == 'P' &&
                buffer[3] == 'F')
            {
                var issue = new ConversionIssue("DET005", "File has .sims3pack extension but contains DBPF magic header.", ConversionIssueSeverity.Warning);
                return new PackageDetectionResult(
                    PackageContainerKind.Dbpf,
                    GameVersion.Unknown,
                    PackageDetectionConfidence.Low,
                    "0", "0", 0,
                    new[] { issue }
                );
            }

            // Parse TS3Pack Binary Header Layout:
            // DWORD signatureLength (4 bytes, uint Little-Endian)
            uint signatureLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(0, 4));

            if (signatureLength >= 7 && signatureLength <= 64 && bytesRead >= 4 + signatureLength + 2 + 4)
            {
                int sigOffset = 4;
                string rawSig = Encoding.ASCII.GetString(buffer, sigOffset, (int)signatureLength);
                string cleanSig = rawSig.TrimEnd('\0', ' ', '\t', '\r', '\n');

                // Strict signature validation: signature MUST equal "TS3Pack" (or null-terminated "TS3Pack\0")
                if (cleanSig.Equals("TS3Pack", StringComparison.Ordinal))
                {
                    int versionOffset = 4 + (int)signatureLength;
                    ushort version = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(versionOffset, 2));

                    int xmlLengthOffset = versionOffset + 2;
                    uint xmlLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(xmlLengthOffset, 4));

                    int headerLength = xmlLengthOffset + 4;
                    long totalRequiredLength = (long)headerLength + xmlLength;

                    // Bounds Check 1: Check xmlLength against stream.Length when stream.CanSeek is true
                    if (canSeek)
                    {
                        if (totalRequiredLength > stream.Length)
                        {
                            var overflowIssue = new ConversionIssue(
                                "DET011",
                                $"TS3Pack header xmlLength ({xmlLength} bytes) extends beyond total stream length ({stream.Length} bytes).",
                                ConversionIssueSeverity.Warning
                            );

                            return new PackageDetectionResult(
                                PackageContainerKind.Sims3Pack,
                                GameVersion.Sims3,
                                PackageDetectionConfidence.Low,
                                version.ToString(),
                                "0",
                                0,
                                new[] { overflowIssue }
                            );
                        }
                    }
                    else
                    {
                        // Non-seekable stream: Total stream length cannot be verified, so High confidence is prohibited
                        var nonSeekableIssue = new ConversionIssue(
                            "DET012",
                            "Stream is non-seekable; total xmlLength boundary cannot be verified against stream length.",
                            ConversionIssueSeverity.Info
                        );

                        return new PackageDetectionResult(
                            PackageContainerKind.Sims3Pack,
                            GameVersion.Sims3,
                            PackageDetectionConfidence.Low,
                            version.ToString(),
                            "0",
                            0,
                            new[] { nonSeekableIssue }
                        );
                    }

                    // Bounds Check 2: Verify XML section preamble at calculated headerLength offset
                    if (bytesRead > headerLength)
                    {
                        string xmlPreamble = Encoding.UTF8.GetString(buffer, headerLength, Math.Min(64, bytesRead - headerLength))
                                                          .TrimStart('\uFEFF', ' ', '\t', '\r', '\n');

                        if (xmlPreamble.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                            xmlPreamble.StartsWith("<", StringComparison.OrdinalIgnoreCase))
                        {
                            return new PackageDetectionResult(
                                PackageContainerKind.Sims3Pack,
                                GameVersion.Sims3,
                                PackageDetectionConfidence.High,
                                version.ToString(),
                                "0",
                                0,
                                Array.Empty<ConversionIssue>()
                            );
                        }
                    }

                    var issueHeader = new ConversionIssue("DET007", $"Valid TS3Pack header (v{version}, xmlLength={xmlLength}B) detected, but XML section preamble is invalid or truncated.", ConversionIssueSeverity.Warning);
                    return new PackageDetectionResult(
                        PackageContainerKind.Sims3Pack,
                        GameVersion.Sims3,
                        PackageDetectionConfidence.Low,
                        version.ToString(),
                        "0",
                        0,
                        new[] { issueHeader }
                    );
                }
            }

            // Plain XML text file without TS3Pack binary header MUST NOT return High confidence
            string cleanText = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (cleanText.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || cleanText.StartsWith("<", StringComparison.OrdinalIgnoreCase))
            {
                var xmlOnlyIssue = new ConversionIssue("DET008", "Plain XML text file detected without binary TS3Pack container header.", ConversionIssueSeverity.Warning);
                return new PackageDetectionResult(
                    isSims3PackExtension ? PackageContainerKind.Sims3Pack : PackageContainerKind.Unknown,
                    isSims3PackExtension ? GameVersion.Sims3 : GameVersion.Unknown,
                    PackageDetectionConfidence.Low,
                    "0", "0", 0,
                    new[] { xmlOnlyIssue }
                );
            }

            if (isSims3PackExtension)
            {
                var issue = new ConversionIssue("DET006", "File has .sims3pack extension but header signature is not a valid TS3Pack binary container.", ConversionIssueSeverity.Warning);
                return new PackageDetectionResult(
                    PackageContainerKind.Sims3Pack,
                    GameVersion.Sims3,
                    PackageDetectionConfidence.Low,
                    "0", "0", 0,
                    new[] { issue }
                );
            }

            var unknownIssue = new ConversionIssue("DET009", "Header signature does not match TS3Pack binary container format.", ConversionIssueSeverity.Info);
            return new PackageDetectionResult(
                PackageContainerKind.Unknown,
                GameVersion.Unknown,
                PackageDetectionConfidence.None,
                "0", "0", 0,
                new[] { unknownIssue }
            );
        }
        catch (Exception ex)
        {
            var issue = new ConversionIssue("DET010", $"Error inspecting Sims3Pack container header: {ex.Message}", ConversionIssueSeverity.Error);
            return new PackageDetectionResult(
                PackageContainerKind.Unknown,
                GameVersion.Unknown,
                PackageDetectionConfidence.None,
                "0", "0", 0,
                new[] { issue }
            );
        }
        finally
        {
            if (canSeek && stream.CanSeek)
            {
                stream.Seek(originalPosition, SeekOrigin.Begin);
            }
        }
    }
}
