using SimsConverter.Domain.Enums;

namespace SimsConverter.Package.Models;

public record PackageHeaderSummary(
    string MajorVersion,
    string MinorVersion,
    int IndexEntryCount,
    GameVersion DetectedGameVersion
);
