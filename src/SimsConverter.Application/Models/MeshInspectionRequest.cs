using SimsConverter.Domain.Enums;

namespace SimsConverter.Application.Models;

public record MeshInspectionRequest(
    string PackageFilePath,
    GameVersion GameVersionHint = GameVersion.Unknown
);
