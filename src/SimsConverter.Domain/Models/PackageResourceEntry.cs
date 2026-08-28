using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record PackageResourceEntry(
    PackageResourceId Id,
    long DataOffset,
    uint CompressedSize,
    uint DecompressedSize,
    PackageCompressionKind CompressionKind,
    ushort CompressionFlags
);
