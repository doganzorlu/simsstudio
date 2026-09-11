using SimsConverter.Domain.Enums;

namespace SimsConverter.Application.Models;

public record DecorativeObjectConversionRequest(
    string SourcePackagePath,
    string TargetOutputPath,
    GameVersion TargetGameVersion = GameVersion.Sims4,
    string? ObjectName = null,
    GameVersion SourceGameVersion = GameVersion.Unknown)
{
    public GameVersion EffectiveSourceGameVersion => SourceGameVersion != GameVersion.Unknown
        ? SourceGameVersion
        : (TargetGameVersion == GameVersion.Sims3 ? GameVersion.Sims4 : GameVersion.Sims3);
}
