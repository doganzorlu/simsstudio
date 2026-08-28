using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Contracts;

public interface IPackageResourceExporter
{
    Task<PackageResourceExportResult> ExportAsync(
        PackageResourceExportRequest request,
        CancellationToken cancellationToken = default);
}
