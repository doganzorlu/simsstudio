using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Contracts;

public interface ITs3CatalogMetadataReader
{
    ObjectCatalogMetadata ReadCatalogMetadata(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        string seedIdentity = "DecorativeObject");

    Task<ObjectCatalogMetadata> ReadCatalogMetadataAsync(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        string seedIdentity = "DecorativeObject",
        CancellationToken cancellationToken = default);
}
