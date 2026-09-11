using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Application.Contracts;

public interface IMeshInspectionService
{
    Task<MeshInspectionResult> InspectPackageMeshesAsync(
        MeshInspectionRequest request,
        CancellationToken cancellationToken = default);

    MeshInspectionResult InspectPackageMeshes(
        PackageInspectionResult packageInspection,
        GameVersion gameVersionHint = GameVersion.Unknown);
}
