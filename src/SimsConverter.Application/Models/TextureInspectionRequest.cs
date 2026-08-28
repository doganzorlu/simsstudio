using SimsConverter.Domain.Enums;

namespace SimsConverter.Application.Models;

public record TextureInspectionRequest(
    string PackageFilePath,
    GameVersion GameVersionHint = GameVersion.Unknown
);
