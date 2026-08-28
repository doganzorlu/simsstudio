using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Contracts;

public interface ISims3PackPayloadExporter
{
    Task<Sims3PackPayloadExportResult> ExportAsync(
        Sims3PackPayloadExportRequest request,
        CancellationToken cancellationToken = default);
}
