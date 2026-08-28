using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Application.Contracts;

public interface ITextureInspectionService
{
    Task<TextureInspectionResult> InspectPackageTexturesAsync(
        TextureInspectionRequest request,
        CancellationToken cancellationToken = default);

    TextureInspectionResult InspectPackageTextures(
        PackageInspectionResult packageInspection,
        GameVersion gameVersionHint = GameVersion.Unknown);
}
