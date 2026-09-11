using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Contracts;

/// <summary>
/// Extracts decoded/decompressed payload bytes for inspection and mesh importers.
/// Raw export workflows use separate exporter boundaries (Sims3PackPayloadExporter).
/// </summary>
public interface IPackageResourcePayloadReader
{
    /// <summary>
    /// Reads and decompresses the resource payload from a package file.
    /// </summary>
    PackageResourcePayloadResult ReadPayload(string packageFilePath, PackageResourceEntry entry);

    /// <summary>
    /// Asynchronously reads and decompresses the resource payload from a package file.
    /// </summary>
    Task<PackageResourcePayloadResult> ReadPayloadAsync(string packageFilePath, PackageResourceEntry entry, CancellationToken cancellationToken = default);
}
