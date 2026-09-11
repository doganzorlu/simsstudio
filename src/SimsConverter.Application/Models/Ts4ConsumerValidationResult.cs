using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record Ts4ConsumerValidationResult(
    bool IsSuccess,
    string PackageFilePath,
    int VerifiedResourceCount,
    int VerifiedGraphLinkCount,
    int VerifiedMeshCount,
    int VerifiedTextureCount,
    ObjectCatalogMetadata? CatalogMetadata,
    IReadOnlyList<ConversionIssue> Issues
);
