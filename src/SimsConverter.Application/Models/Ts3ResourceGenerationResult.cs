using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record Ts3ResourceGenerationResult(
    IReadOnlyList<DecorativeObjectPackageWriteResourceEntry> GeneratedResources,
    IReadOnlyList<DecorativeObjectSourceResourceLink> VerifiedLinks,
    IReadOnlyList<ConversionIssue> Issues
);
