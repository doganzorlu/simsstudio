using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface IPackageInspectionService
{
    Task<PackageInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default);
}
