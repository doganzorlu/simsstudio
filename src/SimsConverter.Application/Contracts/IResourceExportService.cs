using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Contracts;

public interface IResourceExportService
{
    Task<PackageResourceExportResult> ExportResourceAsync(
        SingleResourceExportRequest request,
        CancellationToken cancellationToken = default);
}
