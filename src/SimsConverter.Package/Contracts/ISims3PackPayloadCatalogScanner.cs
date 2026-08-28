using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Contracts;

public interface ISims3PackPayloadCatalogScanner
{
    Task<Sims3PackCatalogResult> ScanFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<Sims3PackCatalogResult> ScanAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
