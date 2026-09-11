using System.Collections.Generic;

namespace SimsConverter.Domain.Models;

public record CanonicalMeshValidationResult(
    bool IsSuccess,
    IReadOnlyList<ConversionIssue> Issues
);
