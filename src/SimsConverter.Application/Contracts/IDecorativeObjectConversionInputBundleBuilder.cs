using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Application.Contracts;

public interface IDecorativeObjectConversionInputBundleBuilder
{
    DecorativeObjectConversionInputBundle BuildBundle(
        DecorativeObjectSourceAssetGraph sourceGraph,
        string targetOutputPath,
        GameVersion targetGameVersion = GameVersion.Sims4);

    Task<DecorativeObjectConversionInputBundle> BuildBundleAsync(
        DecorativeObjectSourceAssetGraph sourceGraph,
        string targetOutputPath,
        GameVersion targetGameVersion = GameVersion.Sims4,
        CancellationToken cancellationToken = default);
}
