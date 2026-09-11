using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record PackageResourceRow(
    uint TypeId,
    uint GroupId,
    ulong InstanceId,
    string TypeHex,
    string GroupHex,
    string InstanceHex,
    string FormattedKey,
    long Offset,
    uint CompressedSize,
    uint DecompressedSize,
    PackageCompressionKind CompressionKind,
    string CompressionName
)
{
    public static PackageResourceRow FromEntry(PackageResourceEntry entry)
    {
        return new PackageResourceRow(
            entry.Id.TypeId,
            entry.Id.GroupId,
            entry.Id.InstanceId,
            $"0x{entry.Id.TypeId:X8}",
            $"0x{entry.Id.GroupId:X8}",
            $"0x{entry.Id.InstanceId:X16}",
            entry.Id.FormattedKey,
            entry.DataOffset,
            entry.CompressedSize,
            entry.DecompressedSize,
            entry.CompressionKind,
            entry.CompressionKind.ToString()
        );
    }

    public PackageResourceEntry ToEntry()
    {
        return new PackageResourceEntry(
            new PackageResourceId(TypeId, GroupId, InstanceId),
            Offset,
            CompressedSize,
            DecompressedSize,
            CompressionKind,
            0
        );
    }
}
