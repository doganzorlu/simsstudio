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

public class DbpfPackageParser : IDbpfPackageParser
{
    private static readonly byte[] DbpfMagic = "DBPF"u8.ToArray();

    // DBPF Header Constants
    private const int MinimumHeaderBufferSize = 96;
    private const int MaxAllowedIndexEntries = 5_000_000;

    // DBPF 2.0 (Sims 4) Offsets
    private const int Dbpf2IndexCountOffset = 36;
    private const int Dbpf2IndexOffsetOffset = 40;
    private const int Dbpf2IndexSizeOffset = 44;
    private const int Dbpf2IndexEntrySize = 32;

    // DBPF 1.x (Sims 3) Offsets
    private const int Dbpf1IndexCountOffset = 24;
    private const int Dbpf1IndexOffsetOffset = 32;
    private const int Dbpf1IndexSizeOffset = 36;
    private const int Dbpf1IndexEntrySize = 20;

    public DbpfParseResult Parse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 4)
        {
            return DbpfParseResult.Failure("PARSE001", "Buffer length is less than 4-byte DBPF magic size.");
        }

        if (!buffer[..4].SequenceEqual(DbpfMagic))
        {
            return DbpfParseResult.Failure("PARSE002", "Header magic sequence does not match DBPF container magic 'DBPF'.");
        }

        var issues = new List<ConversionIssue>();

        // Bounds-checked major/minor version reading (guarded for buffer lengths between 4 and 11 bytes)
        int majorVersion = buffer.Length >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4, 4)) : 0;
        int minorVersion = buffer.Length >= 12 ? BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(8, 4)) : 0;

        if (buffer.Length < MinimumHeaderBufferSize)
        {
            issues.Add(new ConversionIssue(
                "PARSE003",
                $"Truncated DBPF header. Expected at least {MinimumHeaderBufferSize} bytes, received {buffer.Length} bytes.",
                ConversionIssueSeverity.Warning
            ));

            var partialHeader = new DbpfHeader("DBPF", majorVersion, minorVersion, 0, 0, 0);
            return new DbpfParseResult(false, partialHeader, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
        }

        int indexEntryCount;
        long indexOffset;
        int indexSizeBytes;
        int entrySize;

        if (majorVersion >= 2)
        {
            indexEntryCount = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(Dbpf2IndexCountOffset, 4));
            indexOffset = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(Dbpf2IndexOffsetOffset, 4));
            indexSizeBytes = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(Dbpf2IndexSizeOffset, 4));
            entrySize = Dbpf2IndexEntrySize;
        }
        else
        {
            indexEntryCount = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(Dbpf1IndexCountOffset, 4));
            indexOffset = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(Dbpf1IndexOffsetOffset, 4));
            indexSizeBytes = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(Dbpf1IndexSizeOffset, 4));
            entrySize = Dbpf1IndexEntrySize;
        }

        var header = new DbpfHeader("DBPF", majorVersion, minorVersion, indexEntryCount, indexOffset, indexSizeBytes);

        if (indexEntryCount < 0 || indexEntryCount > MaxAllowedIndexEntries)
        {
            issues.Add(new ConversionIssue(
                "PARSE004",
                $"Invalid or suspicious index entry count: {indexEntryCount}.",
                ConversionIssueSeverity.Error
            ));
            return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
        }

        if (indexOffset < 0 || indexOffset > int.MaxValue)
        {
            issues.Add(new ConversionIssue(
                "PARSE005",
                $"Invalid or out-of-bounds index offset: {indexOffset}.",
                ConversionIssueSeverity.Error
            ));
            return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
        }

        if (indexSizeBytes < 0)
        {
            issues.Add(new ConversionIssue(
                "PARSE006",
                $"Invalid or negative index size: {indexSizeBytes}.",
                ConversionIssueSeverity.Error
            ));
            return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
        }

        long requiredBytesLong = (long)indexEntryCount * entrySize;
        if (requiredBytesLong > int.MaxValue)
        {
            issues.Add(new ConversionIssue(
                "PARSE004",
                $"Index entry count multiplication overflowed: {indexEntryCount} * {entrySize} bytes.",
                ConversionIssueSeverity.Error
            ));
            return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
        }

        int requiredBytes = (int)requiredBytesLong;
        if (indexSizeBytes < requiredBytes)
        {
            issues.Add(new ConversionIssue(
                "PARSE013",
                $"Header IndexSizeBytes ({indexSizeBytes}) is smaller than total required bytes ({requiredBytes}) for {indexEntryCount} entries.",
                ConversionIssueSeverity.Error
            ));
            return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
        }

        var entries = new List<PackageResourceEntry>();
        long currentOffset = indexOffset;

        for (int i = 0; i < indexEntryCount; i++)
        {
            if (currentOffset + entrySize > buffer.Length)
            {
                issues.Add(new ConversionIssue(
                    "PARSE007",
                    $"Buffer ended prematurely at entry index {i} (expected offset {currentOffset + entrySize}, buffer length {buffer.Length}).",
                    ConversionIssueSeverity.Warning
                ));
                break;
            }

            ReadOnlySpan<byte> entrySpan = buffer.Slice((int)currentOffset, entrySize);
            var entry = ParseEntry(entrySpan, majorVersion);
            entries.Add(entry);

            currentOffset += entrySize;
        }

        bool isSuccess = !issues.Exists(i => i.Severity is ConversionIssueSeverity.Error or ConversionIssueSeverity.Fatal);
        return new DbpfParseResult(isSuccess, header, entries.AsReadOnly(), issues.AsReadOnly());
    }

    private static PackageResourceEntry ParseEntry(ReadOnlySpan<byte> entrySpan, int majorVersion)
    {
        if (majorVersion >= 2)
        {
            uint typeId = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(0, 4));
            uint groupId = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(4, 4));
            ulong instanceId = BinaryPrimitives.ReadUInt64LittleEndian(entrySpan.Slice(8, 8));
            long dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(16, 4));
            uint compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(20, 4));
            uint decompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(24, 4));
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(entrySpan.Slice(28, 2));

            PackageCompressionKind compressionKind = ResolveCompressionKind(flags, compressedSize, decompressedSize);
            var resourceId = new PackageResourceId(typeId, groupId, instanceId);

            return new PackageResourceEntry(
                resourceId,
                dataOffset,
                compressedSize,
                decompressedSize,
                compressionKind,
                flags
            );
        }
        else
        {
            uint typeId = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(0, 4));
            uint groupId = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(4, 4));
            ulong instanceId = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(8, 4));
            long dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(12, 4));
            uint compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(entrySpan.Slice(16, 4));
            uint decompressedSize = compressedSize;

            var resourceId = new PackageResourceId(typeId, groupId, instanceId);
            return new PackageResourceEntry(
                resourceId,
                dataOffset,
                compressedSize,
                decompressedSize,
                PackageCompressionKind.None,
                0
            );
        }
    }

    private static PackageCompressionKind ResolveCompressionKind(ushort flags, uint compressedSize, uint decompressedSize)
    {
        if (flags == 0x0000 && compressedSize == decompressedSize)
        {
            return PackageCompressionKind.None;
        }

        if (flags == 0x5A42) // "ZB" (ZLIB)
        {
            return PackageCompressionKind.Zlib;
        }

        if (flags == 0xFFFE) // RefPack
        {
            return PackageCompressionKind.RefPack;
        }

        return flags > 0 ? PackageCompressionKind.Unknown : PackageCompressionKind.None;
    }

    public async Task<DbpfParseResult> ParseAsync(Stream? stream, CancellationToken cancellationToken = default)
    {
        if (stream == null || !stream.CanRead)
        {
            return DbpfParseResult.Failure("PARSE008", "Target stream is null or unreadable.");
        }

        try
        {
            byte[] headerBuffer = new byte[MinimumHeaderBufferSize];
            int headerBytesRead = 0;

            while (headerBytesRead < MinimumHeaderBufferSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(
                    headerBuffer.AsMemory(headerBytesRead, MinimumHeaderBufferSize - headerBytesRead),
                    cancellationToken
                );

                if (read == 0)
                {
                    break;
                }

                headerBytesRead += read;
            }

            if (headerBytesRead < 4)
            {
                return DbpfParseResult.Failure("PARSE001", "Stream length is less than 4-byte DBPF magic size.");
            }

            if (!headerBuffer.AsSpan(0, 4).SequenceEqual(DbpfMagic))
            {
                return DbpfParseResult.Failure("PARSE002", "Header magic sequence does not match DBPF container magic 'DBPF'.");
            }

            int majorVersion = headerBytesRead >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(4, 4)) : 0;
            int minorVersion = headerBytesRead >= 12 ? BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(8, 4)) : 0;

            if (headerBytesRead < MinimumHeaderBufferSize)
            {
                var partialHeader = new DbpfHeader("DBPF", majorVersion, minorVersion, 0, 0, 0);
                var issue = new ConversionIssue("PARSE003", $"Truncated DBPF stream header. Received {headerBytesRead} bytes.", ConversionIssueSeverity.Warning);
                return new DbpfParseResult(false, partialHeader, Array.Empty<PackageResourceEntry>(), new[] { issue });
            }

            int indexEntryCount = majorVersion >= 2
                ? BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(Dbpf2IndexCountOffset, 4))
                : BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(Dbpf1IndexCountOffset, 4));

            long indexOffset = majorVersion >= 2
                ? BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(Dbpf2IndexOffsetOffset, 4))
                : BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(Dbpf1IndexOffsetOffset, 4));

            int indexSizeBytes = majorVersion >= 2
                ? BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(Dbpf2IndexSizeOffset, 4))
                : BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(Dbpf1IndexSizeOffset, 4));

            var header = new DbpfHeader("DBPF", majorVersion, minorVersion, indexEntryCount, indexOffset, indexSizeBytes);
            var issues = new List<ConversionIssue>();

            if (indexEntryCount < 0 || indexEntryCount > MaxAllowedIndexEntries)
            {
                issues.Add(new ConversionIssue("PARSE004", $"Invalid or negative index entry count: {indexEntryCount}.", ConversionIssueSeverity.Error));
                return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
            }

            if (indexOffset < 0 || (stream.CanSeek && indexOffset > stream.Length))
            {
                issues.Add(new ConversionIssue("PARSE005", $"Index offset {indexOffset} is outside stream bounds.", ConversionIssueSeverity.Error));
                return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
            }

            if (indexSizeBytes < 0)
            {
                issues.Add(new ConversionIssue("PARSE006", $"Invalid or negative index size: {indexSizeBytes}.", ConversionIssueSeverity.Error));
                return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
            }

            int entrySize = majorVersion >= 2 ? Dbpf2IndexEntrySize : Dbpf1IndexEntrySize;
            long totalRequiredIndexBytesLong = (long)indexEntryCount * entrySize;

            if (totalRequiredIndexBytesLong > int.MaxValue)
            {
                issues.Add(new ConversionIssue("PARSE004", $"Index entry count multiplication overflowed: {indexEntryCount} * {entrySize} bytes.", ConversionIssueSeverity.Error));
                return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
            }

            int totalRequiredIndexBytes = (int)totalRequiredIndexBytesLong;
            if (indexSizeBytes < totalRequiredIndexBytes)
            {
                issues.Add(new ConversionIssue(
                    "PARSE013",
                    $"Header IndexSizeBytes ({indexSizeBytes}) is smaller than total required bytes ({totalRequiredIndexBytes}) for {indexEntryCount} entries.",
                    ConversionIssueSeverity.Error
                ));
                return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
            }

            if (!stream.CanSeek)
            {
                if (indexOffset < headerBytesRead)
                {
                    return DbpfParseResult.Failure("PARSE014", $"Cannot seek backwards to index offset {indexOffset} on non-seekable stream (current position: {headerBytesRead}).");
                }

                long bytesToDiscard = indexOffset - headerBytesRead;
                byte[] discardBuffer = new byte[Math.Min(4096, (int)Math.Min(bytesToDiscard, 65536))];
                long discarded = 0;

                while (discarded < bytesToDiscard)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int toRead = (int)Math.Min(discardBuffer.Length, bytesToDiscard - discarded);
                    int read = await stream.ReadAsync(discardBuffer.AsMemory(0, toRead), cancellationToken);
                    if (read == 0)
                    {
                        issues.Add(new ConversionIssue("PARSE005", $"Stream ended while skipping to index offset {indexOffset}.", ConversionIssueSeverity.Error));
                        return new DbpfParseResult(false, header, Array.Empty<PackageResourceEntry>(), issues.AsReadOnly());
                    }
                    discarded += read;
                }
            }
            else
            {
                stream.Seek(indexOffset, SeekOrigin.Begin);
            }

            byte[] indexData = new byte[totalRequiredIndexBytes];
            int indexBytesRead = 0;

            while (indexBytesRead < totalRequiredIndexBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(
                    indexData.AsMemory(indexBytesRead, totalRequiredIndexBytes - indexBytesRead),
                    cancellationToken
                );

                if (read == 0)
                {
                    issues.Add(new ConversionIssue(
                        "PARSE007",
                        $"Stream ended prematurely while reading index entries (read {indexBytesRead} of {totalRequiredIndexBytes} bytes).",
                        ConversionIssueSeverity.Warning
                    ));
                    break;
                }

                indexBytesRead += read;
            }

            int parsedEntriesCount = indexBytesRead / entrySize;
            var entries = new List<PackageResourceEntry>(parsedEntriesCount);

            for (int i = 0; i < parsedEntriesCount; i++)
            {
                ReadOnlySpan<byte> entrySpan = indexData.AsSpan(i * entrySize, entrySize);
                entries.Add(ParseEntry(entrySpan, majorVersion));
            }

            bool isSuccess = !issues.Exists(i => i.Severity is ConversionIssueSeverity.Error or ConversionIssueSeverity.Fatal);
            return new DbpfParseResult(isSuccess, header, entries.AsReadOnly(), issues.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DbpfParseResult.Failure("PARSE009", $"Stream parsing failed with error: {ex.Message}");
        }
    }

    public async Task<DbpfParseResult> ParseFileAsync(string? filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return DbpfParseResult.Failure("PARSE010", "Specified file path is null, empty, or whitespace.");
        }

        if (!File.Exists(filePath))
        {
            return DbpfParseResult.Failure("PARSE011", $"File not found at path '{filePath}'.");
        }

        try
        {
            // Open strictly in Read mode with FileShare.Read to ensure source file is NEVER modified or locked exclusively
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true
            );

            return await ParseAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DbpfParseResult.Failure("PARSE012", $"Failed to parse package file '{filePath}': {ex.Message}");
        }
    }
}
