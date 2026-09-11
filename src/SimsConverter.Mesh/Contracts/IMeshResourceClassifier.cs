using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Contracts;

public interface IMeshResourceClassifier
{
    MeshResourceClassification Classify(PackageResourceEntry entry, GameVersion gameVersionHint = GameVersion.Unknown);
    IReadOnlyList<MeshResourceClassification> ClassifyBatch(IEnumerable<PackageResourceEntry> entries, GameVersion gameVersionHint = GameVersion.Unknown);
}
