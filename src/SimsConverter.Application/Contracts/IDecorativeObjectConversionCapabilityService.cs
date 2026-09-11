using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Models;

namespace SimsConverter.Application.Contracts;

/// <summary>
/// Service interface for evaluating bidirectional conversion capability matrix for TS3 <-> TS4 decorative objects.
/// </summary>
public interface IDecorativeObjectConversionCapabilityService
{
    /// <summary>
    /// Evaluates the conversion capability of resources present in a package for the specified conversion direction.
    /// </summary>
    DecorativeObjectConversionCapabilityMatrix EvaluateCapability(
        PackageInspectionResult packageResult,
        GameVersion sourceVersion,
        GameVersion targetVersion);
}
