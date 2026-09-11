using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Models;

namespace SimsConverter.Package.Services;

public class DbpfPackageWriter : IDbpfPackageWriter
{
    private static readonly byte[] DbpfMagic = "DBPF"u8.ToArray();
    private const int DbpfHeaderSize = 96;
    private const int Dbpf2IndexEntrySize = 32;

    public async Task<DbpfPackageWriteResult> WritePackageAsync(
        string sourcePackagePath,
        string targetOutputPath,
        IEnumerable<DbpfPackageWriteResourceEntry> resources,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => WritePackage(sourcePackagePath, targetOutputPath, resources), cancellationToken).ConfigureAwait(false);
    }

    public DbpfPackageWriteResult WritePackage(
        string sourcePackagePath,
        string targetOutputPath,
        IEnumerable<DbpfPackageWriteResourceEntry> resources)
    {
        var issues = new List<ConversionIssue>();

        if (resources == null)
        {
            issues.Add(new ConversionIssue("WRIT000", "Package write resource list is null.", ConversionIssueSeverity.Error));
            return new DbpfPackageWriteResult(false, string.Empty, 0, 0, issues.AsReadOnly());
        }

        var resourceList = resources.ToList();

        if (string.IsNullOrWhiteSpace(sourcePackagePath) || string.IsNullOrWhiteSpace(targetOutputPath))
        {
            issues.Add(new ConversionIssue("WRIT001", "Source package path or target output path is empty.", ConversionIssueSeverity.Error));
            return new DbpfPackageWriteResult(false, targetOutputPath ?? string.Empty, 0, 0, issues.AsReadOnly());
        }

        string fullSourcePath;
        string fullTargetPath;

        try
        {
            fullSourcePath = Path.GetFullPath(sourcePackagePath);
            fullTargetPath = Path.GetFullPath(targetOutputPath);
        }
        catch (Exception ex)
        {
            issues.Add(new ConversionIssue("WRIT001", $"Invalid package path: {ex.Message}", ConversionIssueSeverity.Error));
            return new DbpfPackageWriteResult(false, targetOutputPath, 0, 0, issues.AsReadOnly());
        }

        // Overwrite Guard: Prohibit writing directly over source package file
        if (string.Equals(fullSourcePath, fullTargetPath, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ConversionIssue(
                "WRIT001",
                $"Target output path '{targetOutputPath}' is identical to source package path '{sourcePackagePath}'. Overwriting source package is prohibited.",
                ConversionIssueSeverity.Error
            ));
            return new DbpfPackageWriteResult(false, fullTargetPath, 0, 0, issues.AsReadOnly());
        }

        string? targetDirectory = Path.GetDirectoryName(fullTargetPath);
        if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
        {
            try
            {
                Directory.CreateDirectory(targetDirectory);
            }
            catch (Exception ex)
            {
                issues.Add(new ConversionIssue("WRIT002", $"Failed to create target output directory '{targetDirectory}': {ex.Message}", ConversionIssueSeverity.Error));
                return new DbpfPackageWriteResult(false, fullTargetPath, 0, 0, issues.AsReadOnly());
            }
        }

        string tempFilePath = fullTargetPath + ".tmp." + Guid.NewGuid().ToString("N");
        long totalBytesWritten = 0;
        int resourceCount = resourceList.Count;

        try
        {
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] headerBuffer = new byte[DbpfHeaderSize];
                Array.Clear(headerBuffer, 0, DbpfHeaderSize);

                // 0..4: Magic "DBPF"
                DbpfMagic.CopyTo(headerBuffer, 0);

                // 4..8: Major Version 2
                BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(4, 4), 2);

                // 8..12: Minor Version 0
                BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(8, 4), 0);

                // 36..40: Index Count
                BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(36, 4), resourceCount);

                // 40..44: Index Offset (placeholder, written at end)
                BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(40, 4), 0);

                // 44..48: Index Size in Bytes
                int indexSizeBytes = resourceCount * Dbpf2IndexEntrySize;
                BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(44, 4), indexSizeBytes);

                fileStream.Write(headerBuffer, 0, DbpfHeaderSize);

                var writtenEntryMetadata = new List<(PackageResourceId Id, uint Offset, uint CompressedSize, uint DecompressedSize, ushort Flags)>();

                foreach (var resource in resourceList)
                {
                    uint offset = (uint)fileStream.Position;
                    byte[] payloadBytes = resource.Payload != null ? resource.Payload.ToArray() : Array.Empty<byte>();

                    if (payloadBytes.Length > 0)
                    {
                        fileStream.Write(payloadBytes, 0, payloadBytes.Length);
                    }

                    uint compSize = (uint)payloadBytes.Length;
                    uint decompSize = resource.DecompressedSize > 0 ? resource.DecompressedSize : compSize;

                    writtenEntryMetadata.Add((resource.ResourceId, offset, compSize, decompSize, 0));
                }

                uint indexOffset = (uint)fileStream.Position;

                byte[] indexEntryBuffer = new byte[Dbpf2IndexEntrySize];

                foreach (var meta in writtenEntryMetadata)
                {
                    Array.Clear(indexEntryBuffer, 0, Dbpf2IndexEntrySize);

                    // 0..4: TypeId
                    BinaryPrimitives.WriteUInt32LittleEndian(indexEntryBuffer.AsSpan(0, 4), meta.Id.TypeId);

                    // 4..8: GroupId
                    BinaryPrimitives.WriteUInt32LittleEndian(indexEntryBuffer.AsSpan(4, 4), meta.Id.GroupId);

                    // 8..16: InstanceId
                    BinaryPrimitives.WriteUInt64LittleEndian(indexEntryBuffer.AsSpan(8, 8), meta.Id.InstanceId);

                    // 16..20: DataOffset
                    BinaryPrimitives.WriteUInt32LittleEndian(indexEntryBuffer.AsSpan(16, 4), meta.Offset);

                    // 20..24: CompressedSize (uncompressed bit 31 is 0)
                    BinaryPrimitives.WriteUInt32LittleEndian(indexEntryBuffer.AsSpan(20, 4), meta.CompressedSize);

                    // 24..28: DecompressedSize
                    BinaryPrimitives.WriteUInt32LittleEndian(indexEntryBuffer.AsSpan(24, 4), meta.DecompressedSize);

                    // 28..30: Flags
                    BinaryPrimitives.WriteUInt16LittleEndian(indexEntryBuffer.AsSpan(28, 2), meta.Flags);

                    // 30..32: Reserved/ComprCapacity
                    BinaryPrimitives.WriteUInt16LittleEndian(indexEntryBuffer.AsSpan(30, 2), 0);

                    fileStream.Write(indexEntryBuffer, 0, Dbpf2IndexEntrySize);
                }

                totalBytesWritten = fileStream.Position;

                // Seek back to header index offset location (offset 40) and write final index offset
                fileStream.Seek(40, SeekOrigin.Begin);
                byte[] offsetBuffer = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(offsetBuffer, indexOffset);
                fileStream.Write(offsetBuffer, 0, 4);

                fileStream.Flush();
            }

            // Atomic Move / Replace to final target output path
            File.Move(tempFilePath, fullTargetPath, overwrite: true);

            return new DbpfPackageWriteResult(
                IsSuccess: true,
                TargetOutputPath: fullTargetPath,
                ResourceCount: resourceCount,
                BytesWritten: totalBytesWritten,
                Issues: issues.AsReadOnly()
            );
        }
        catch (Exception ex)
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore secondary cleanup exception
                }
            }

            issues.Add(new ConversionIssue(
                "WRIT002",
                $"Failed to write TS4 package container file '{fullTargetPath}': {ex.Message}",
                ConversionIssueSeverity.Error
            ));

            return new DbpfPackageWriteResult(
                IsSuccess: false,
                TargetOutputPath: fullTargetPath,
                ResourceCount: 0,
                BytesWritten: 0,
                Issues: issues.AsReadOnly()
            );
        }
    }
}
