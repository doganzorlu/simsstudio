using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Textures.Contracts;

public interface ITextureResourceClassifier
{
    TextureResourceClassification Classify(
        PackageResourceEntry entry,
        GameVersion gameVersionHint = GameVersion.Unknown);

    IReadOnlyList<TextureResourceClassification> ClassifyBatch(
        IEnumerable<PackageResourceEntry> entries,
        GameVersion gameVersionHint = GameVersion.Unknown);
}
