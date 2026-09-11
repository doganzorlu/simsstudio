using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Models;

public record Ts3GeomParseResult(
    bool IsSuccess,
    Ts3GeomMetadata? Metadata,
    IReadOnlyList<ConversionIssue> Issues
);
