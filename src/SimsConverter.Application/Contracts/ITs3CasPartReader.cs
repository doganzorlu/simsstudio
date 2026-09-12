using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface ITs3CasPartReader
{
    CasPartMetadata ReadCasPartMetadata(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        string seedIdentity = "CasPart");

    Task<CasPartMetadata> ReadCasPartMetadataAsync(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        string seedIdentity = "CasPart",
        CancellationToken cancellationToken = default);
}
