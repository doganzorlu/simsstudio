using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Models;

public record DbpfPackageWriteResourceEntry(
    PackageResourceId ResourceId,
    IReadOnlyList<byte> Payload,
    PackageCompressionKind CompressionKind = PackageCompressionKind.None,
    uint DecompressedSize = 0
);
