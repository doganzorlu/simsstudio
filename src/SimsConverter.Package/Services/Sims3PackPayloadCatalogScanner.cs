using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Package.Services;

public class Sims3PackPayloadCatalogScanner : ISims3PackPayloadCatalogScanner
{
    private static readonly byte[] DbpfMagic = "DBPF"u8.ToArray();
    private static readonly byte[] PngMagic = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public const int MaxCatalogEntries = 100;
    public const long MaxXmlMetadataBytes = 10 * 1024 * 1024; // 10 MB limit for XML skip
    public const long MaxArchiveScanBytes = 50 * 1024 * 1024; // 50 MB scan limit
    private const int MinDbpfHeaderSize = 96;
    private const int ChunkSize = 8192;
    private const int OverlapSize = 7; // PNG magic is 8 bytes, so 7 overlap bytes guarantees no split signature missed

    public async Task<Sims3PackCatalogResult> ScanFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Sims3PackCatalogResult.Failure("S3PC000", "File path is null, empty, or file does not exist.");
        }

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return await ScanAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            return Sims3PackCatalogResult.Failure("S3PC009", $"File access error during Sims3Pack archive catalog scan: {ex.Message}");
        }
    }

    public async Task<Sims3PackCatalogResult> ScanAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (stream == null || !stream.CanRead)
        {
            return Sims3PackCatalogResult.Failure("S3PC000", "Stream is null or unreadable.");
        }

        try
        {
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            // Step 1: Read outer header to locate archiveOffset
            byte[] headerLenBuffer = new byte[4];
            int read = await ReadExactAsync(stream, headerLenBuffer, cancellationToken);
            if (read < 4)
            {
                return Sims3PackCatalogResult.Failure("S3PC004", "Truncated Sims3Pack outer header.");
            }

            uint signatureLength = BinaryPrimitives.ReadUInt32LittleEndian(headerLenBuffer);
            if (signatureLength < 7 || signatureLength > 64)
            {
                return Sims3PackCatalogResult.Failure("S3PC005", $"Invalid TS3Pack signature length: {signatureLength}.");
            }

            byte[] sigBuffer = new byte[signatureLength];
            read = await ReadExactAsync(stream, sigBuffer, cancellationToken);
            if (read < signatureLength)
            {
                return Sims3PackCatalogResult.Failure("S3PC004", "Truncated TS3Pack signature string.");
            }

            string rawSig = Encoding.ASCII.GetString(sigBuffer);
            if (!rawSig.Equals("TS3Pack", StringComparison.Ordinal) && !rawSig.Equals("TS3Pack\0", StringComparison.Ordinal))
            {
                return Sims3PackCatalogResult.Failure("S3PC005", $"Invalid TS3Pack signature string: '{rawSig}'.");
            }

            byte[] versionAndXmlLenBuffer = new byte[6];
            read = await ReadExactAsync(stream, versionAndXmlLenBuffer, cancellationToken);
            if (read < 6)
            {
                return Sims3PackCatalogResult.Failure("S3PC004", "Truncated TS3Pack version/xmlLength header.");
            }

            ushort version = BinaryPrimitives.ReadUInt16LittleEndian(versionAndXmlLenBuffer.AsSpan(0, 2));
            uint xmlLength = BinaryPrimitives.ReadUInt32LittleEndian(versionAndXmlLenBuffer.AsSpan(2, 4));

            // Guard 1: xmlLength upper limit check
            if (xmlLength > MaxXmlMetadataBytes)
            {
                return Sims3PackCatalogResult.Failure(
                    "S3PC007",
                    $"XML metadata section length ({xmlLength} bytes) exceeds maximum allowed limit of {MaxXmlMetadataBytes} bytes."
                );
            }

            long archiveOffset = 4 + signatureLength + 2 + 4 + xmlLength;

            // Guard 2: xmlLength extends beyond stream boundary
            if (stream.CanSeek && archiveOffset > stream.Length)
            {
                return Sims3PackCatalogResult.Failure("S3PC003", $"XML length ({xmlLength} bytes) extends beyond stream boundary.");
            }

            // Step 2: Skip XML section without allocating large array
            if (stream.CanSeek)
            {
                stream.Seek(archiveOffset, SeekOrigin.Begin);
            }
            else
            {
                byte[] dummyBuffer = new byte[4096];
                long remainingXmlToSkip = xmlLength;
                while (remainingXmlToSkip > 0)
                {
                    int toRead = (int)Math.Min(dummyBuffer.Length, remainingXmlToSkip);
                    int skipped = await stream.ReadAsync(dummyBuffer.AsMemory(0, toRead), cancellationToken);
                    if (skipped == 0)
                    {
                        return Sims3PackCatalogResult.Failure("S3PC003", "Premature end of stream while skipping XML metadata section.");
                    }
                    remainingXmlToSkip -= skipped;
                }
            }

            // Step 3: Chunked streaming scan over archive section
            var catalogEntries = new List<Sims3PackCatalogEntry>();
            var generalIssues = new List<ConversionIssue>();
            var detectedOffsets = new HashSet<long>();

            long archiveBytesScanned = 0;
            long totalStreamLength = stream.CanSeek ? stream.Length : -1;

            byte[] chunkBuffer = new byte[ChunkSize];
            int currentChunkLength = 0;
            long currentChunkStartOffset = archiveOffset;

            int entryCounter = 1;
            bool hitScanLimit = false;
            bool hitEntryLimit = false;

            while (catalogEntries.Count < MaxCatalogEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (archiveBytesScanned >= MaxArchiveScanBytes)
                {
                    hitScanLimit = true;
                    break;
                }

                int spaceToRead = (int)Math.Min(ChunkSize - currentChunkLength, MaxArchiveScanBytes - archiveBytesScanned);
                if (spaceToRead <= 0)
                {
                    hitScanLimit = true;
                    break;
                }

                int bytesRead = await stream.ReadAsync(chunkBuffer.AsMemory(currentChunkLength, spaceToRead), cancellationToken);
                if (bytesRead == 0)
                {
                    break; // End of stream reached
                }

                archiveBytesScanned += bytesRead;
                currentChunkLength += bytesRead;

                int i = 0;
                int scanLimitIndex = currentChunkLength - 4; // Need at least 4 bytes to check DBPF magic

                while (i <= scanLimitIndex)
                {
                    if (catalogEntries.Count >= MaxCatalogEntries)
                    {
                        hitEntryLimit = true;
                        break;
                    }

                    ReadOnlySpan<byte> window = chunkBuffer.AsSpan(i, currentChunkLength - i);

                    // Check DBPF candidate
                    if (window.Length >= 4 && window[..4].SequenceEqual(DbpfMagic))
                    {
                        long candidateOffset = currentChunkStartOffset + i;

                        // De-duplication check: Skip if this offset was already recorded
                        if (!detectedOffsets.Contains(candidateOffset))
                        {
                            detectedOffsets.Add(candidateOffset);
                            long? estimatedSize = totalStreamLength > 0 ? totalStreamLength - candidateOffset : null;

                            // Peek and validate DBPF header candidate
                            byte[] candidateHeader = new byte[MinDbpfHeaderSize];
                            int headerBytesRead = 0;

                            if (stream.CanSeek)
                            {
                                long savedPos = stream.Position;
                                stream.Seek(candidateOffset, SeekOrigin.Begin);
                                headerBytesRead = await ReadExactAsync(stream, candidateHeader, cancellationToken);
                                stream.Seek(savedPos, SeekOrigin.Begin);
                            }
                            else
                            {
                                int available = Math.Min(MinDbpfHeaderSize, currentChunkLength - i);
                                chunkBuffer.AsSpan(i, available).CopyTo(candidateHeader);
                                headerBytesRead = available;
                            }

                            bool isValidDbpf = ValidateDbpfHeader(
                                candidateHeader.AsSpan(0, headerBytesRead),
                                candidateOffset,
                                totalStreamLength,
                                out var headerIssues
                            );

                            Sims3PackPayloadKind candidateKind = isValidDbpf
                                ? Sims3PackPayloadKind.DbpfPackage
                                : Sims3PackPayloadKind.InvalidDbpfPackage;

                            string candidateDisplayName = isValidDbpf
                                ? $"Embedded Package #{entryCounter}"
                                : $"Invalid DBPF Candidate #{entryCounter}";

                            catalogEntries.Add(new Sims3PackCatalogEntry(
                                EntryIndex: entryCounter++,
                                Kind: candidateKind,
                                DataOffset: candidateOffset,
                                EstimatedSizeBytes: estimatedSize,
                                DisplayName: candidateDisplayName,
                                Issues: headerIssues.AsReadOnly()
                            ));
                        }

                        i += 4;
                        continue;
                    }

                    // Check PNG candidate
                    if (window.Length >= 8 && window[..8].SequenceEqual(PngMagic))
                    {
                        long candidateOffset = currentChunkStartOffset + i;

                        if (!detectedOffsets.Contains(candidateOffset))
                        {
                            detectedOffsets.Add(candidateOffset);
                            long? estimatedSize = totalStreamLength > 0 ? totalStreamLength - candidateOffset : null;

                            catalogEntries.Add(new Sims3PackCatalogEntry(
                                EntryIndex: entryCounter++,
                                Kind: Sims3PackPayloadKind.PngPreview,
                                DataOffset: candidateOffset,
                                EstimatedSizeBytes: estimatedSize,
                                DisplayName: $"Preview Image #{entryCounter - 1}",
                                Issues: Array.Empty<ConversionIssue>()
                            ));
                        }

                        i += 8;
                        continue;
                    }

                    i++;
                }

                if (hitEntryLimit)
                {
                    break;
                }

                // Prepare overlap buffer for next chunk scan loop
                int keepOverlap = Math.Min(OverlapSize, currentChunkLength);
                if (keepOverlap > 0)
                {
                    Array.Copy(chunkBuffer, currentChunkLength - keepOverlap, chunkBuffer, 0, keepOverlap);
                    currentChunkStartOffset += (currentChunkLength - keepOverlap);
                    currentChunkLength = keepOverlap;
                }
                else
                {
                    currentChunkStartOffset += currentChunkLength;
                    currentChunkLength = 0;
                }
            }

            if (hitEntryLimit)
            {
                generalIssues.Add(new ConversionIssue(
                    "S3PC008",
                    $"Catalog entry count reached maximum limit of {MaxCatalogEntries} entries. Further candidates were omitted.",
                    ConversionIssueSeverity.Warning
                ));
            }
            else if (hitScanLimit)
            {
                generalIssues.Add(new ConversionIssue(
                    "S3PC006",
                    $"Archive scan reached maximum limit of {MaxArchiveScanBytes / (1024 * 1024)}MB. Further payload candidates were omitted.",
                    ConversionIssueSeverity.Warning
                ));
            }

            // Ensure catalog entries are deterministically ordered by DataOffset
            var orderedEntries = catalogEntries.OrderBy(e => e.DataOffset).ToList();
            for (int k = 0; k < orderedEntries.Count; k++)
            {
                orderedEntries[k] = orderedEntries[k] with { EntryIndex = k + 1 };
            }

            return new Sims3PackCatalogResult(
                true,
                orderedEntries.AsReadOnly(),
                generalIssues.AsReadOnly()
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Sims3PackCatalogResult.Failure("S3PC009", $"Error scanning Sims3Pack archive catalog: {ex.Message}");
        }
    }

    public static bool ValidateDbpfHeader(
        ReadOnlySpan<byte> headerSpan,
        long candidateOffset,
        long totalStreamLength,
        out List<ConversionIssue> issues)
    {
        issues = new List<ConversionIssue>();

        if (headerSpan.Length < 4 || !headerSpan[..4].SequenceEqual(DbpfMagic))
        {
            issues.Add(new ConversionIssue("S3PC010", $"Candidate at offset 0x{candidateOffset:X8} does not start with DBPF magic header.", ConversionIssueSeverity.Warning));
            return false;
        }

        if (headerSpan.Length < MinDbpfHeaderSize)
        {
            issues.Add(new ConversionIssue("S3PC010", $"Candidate DBPF header at offset 0x{candidateOffset:X8} is truncated (received {headerSpan.Length} bytes, expected at least {MinDbpfHeaderSize} bytes).", ConversionIssueSeverity.Warning));
            return false;
        }

        int majorVersion = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(4, 4));
        int minorVersion = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(8, 4));

        int minHeaderSize = majorVersion >= 2 ? MinDbpfHeaderSize : 32;
        int indexEntryCount;
        long indexOffset;
        int indexSizeBytes;
        int entrySize;

        if (majorVersion >= 2)
        {
            indexEntryCount = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(36, 4));
            indexOffset = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(40, 4));
            if (indexOffset == 0 && headerSpan.Length >= 68)
            {
                indexOffset = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(64, 4));
            }
            indexSizeBytes = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(44, 4));
            entrySize = 32;
        }
        else
        {
            indexEntryCount = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(24, 4));
            indexOffset = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(32, 4));
            indexSizeBytes = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(36, 4));
            entrySize = 20;
        }

        if (indexEntryCount <= 0 || indexEntryCount > 5_000_000)
        {
            issues.Add(new ConversionIssue(
                "S3PC010",
                $"Invalid embedded DBPF header at offset 0x{candidateOffset:X8}: indexEntryCount ({indexEntryCount}) must be positive and non-zero.",
                ConversionIssueSeverity.Warning
            ));
            return false;
        }

        long effectiveIndexOffset = indexOffset;
        if (indexOffset == 0)
        {
            effectiveIndexOffset = MinDbpfHeaderSize;
        }
        else if (indexOffset < minHeaderSize)
        {
            issues.Add(new ConversionIssue(
                "S3PC010",
                $"Invalid embedded DBPF header at offset 0x{candidateOffset:X8}: indexOffset ({indexOffset}) points inside DBPF header (minimum header size is {minHeaderSize} bytes).",
                ConversionIssueSeverity.Warning
            ));
            return false;
        }

        if (indexSizeBytes < 0)
        {
            issues.Add(new ConversionIssue(
                "S3PC010",
                $"Invalid embedded DBPF header at offset 0x{candidateOffset:X8}: indexSizeBytes ({indexSizeBytes}) is negative.",
                ConversionIssueSeverity.Warning
            ));
            return false;
        }

        long requiredIndexBytes = (long)indexEntryCount * entrySize;
        if (indexSizeBytes < requiredIndexBytes)
        {
            issues.Add(new ConversionIssue(
                "S3PC010",
                $"Invalid embedded DBPF header at offset 0x{candidateOffset:X8}: indexSizeBytes ({indexSizeBytes}) is smaller than required bytes ({requiredIndexBytes}) for {indexEntryCount} entries.",
                ConversionIssueSeverity.Warning
            ));
            return false;
        }

        if (totalStreamLength > 0)
        {
            long availableBytesFromCandidate = totalStreamLength - candidateOffset;
            if (availableBytesFromCandidate < minHeaderSize)
            {
                issues.Add(new ConversionIssue(
                    "S3PC010",
                    $"Invalid embedded DBPF header at offset 0x{candidateOffset:X8}: remaining archive size ({availableBytesFromCandidate} bytes) is less than minimum DBPF header size ({minHeaderSize} bytes).",
                    ConversionIssueSeverity.Warning
                ));
                return false;
            }

            if (effectiveIndexOffset + requiredIndexBytes > availableBytesFromCandidate)
            {
                issues.Add(new ConversionIssue(
                    "S3PC010",
                    $"Invalid embedded DBPF header at offset 0x{candidateOffset:X8}: index table range ({effectiveIndexOffset} + {requiredIndexBytes}) extends beyond archive file end.",
                    ConversionIssueSeverity.Warning
                ));
                return false;
            }
        }

        return true;
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }
            totalRead += read;
        }
        return totalRead;
    }
}
