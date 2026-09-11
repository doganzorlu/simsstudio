using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Application.Contracts;

public interface IDecorativeObjectSourceGraphBuilder
{
    Task<DecorativeObjectSourceAssetGraph> BuildGraphAsync(
        string packageFilePath,
        GameVersion sourceGameVersionHint = GameVersion.Unknown,
        CancellationToken cancellationToken = default);

    DecorativeObjectSourceAssetGraph BuildGraph(
        PackageInspectionResult packageInspection,
        GameVersion sourceGameVersionHint = GameVersion.Unknown);
}
