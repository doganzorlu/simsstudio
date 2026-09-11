using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectPackageWriteResourceEntry(
    PackageResourceId ResourceId,
    string FormattedKey,
    IReadOnlyList<byte> Payload,
    PackageCompressionKind CompressionKind,
    uint DecompressedSize,
    uint CompressedSize
);
