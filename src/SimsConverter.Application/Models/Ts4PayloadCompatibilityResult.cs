using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record Ts4PayloadCompatibilityResult(
    bool IsSuccess,
    int TotalResourcesVerified,
    int VerifiedTgiLinkCount,
    IReadOnlyList<ConversionIssue> Issues
);
