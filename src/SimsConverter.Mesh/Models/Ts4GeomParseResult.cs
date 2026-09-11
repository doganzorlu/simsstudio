using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Models;

public record Ts4GeomParseResult(
    bool IsSuccess,
    Ts4GeomMetadata? Metadata,
    IReadOnlyList<ConversionIssue> Issues
);
