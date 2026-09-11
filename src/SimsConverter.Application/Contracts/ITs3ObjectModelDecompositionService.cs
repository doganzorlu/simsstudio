using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

/// <summary>
/// Service contract for decomposing TS3 Object Model resources (MODL, MLOD, RIG, RSLT) from target package files.
/// Uses IPackageResourcePayloadReader for decoded/decompressed payload extraction and aggregates ITs3ObjectModelMetadataReader results.
/// </summary>
public interface ITs3ObjectModelDecompositionService
{
    /// <summary>
    /// Decomposes TS3 Object Model resources from a package file asynchronously.
    /// </summary>
    Task<Ts3ObjectModelDecompositionResult> DecomposeAsync(
        string packageFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decomposes TS3 Object Model resources synchronously given an inspected package result.
    /// </summary>
    Ts3ObjectModelDecompositionResult Decompose(
        PackageInspectionResult packageInspection);
}
