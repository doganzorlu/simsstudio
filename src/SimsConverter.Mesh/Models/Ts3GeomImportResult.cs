using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Models;

public record Ts3GeomImportResult(
    bool IsSuccess,
    CanonicalMesh? Mesh,
    IReadOnlyList<ConversionIssue> Issues
);
