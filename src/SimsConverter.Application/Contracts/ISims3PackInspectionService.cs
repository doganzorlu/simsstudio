using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface ISims3PackInspectionService
{
    Task<Sims3PackInspectionResult> InspectFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<Sims3PackExportResult> ExportPayloadAsync(
        Sims3PackExportRequest request,
        CancellationToken cancellationToken = default);
}
