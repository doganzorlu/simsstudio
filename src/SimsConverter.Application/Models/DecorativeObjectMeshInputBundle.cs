using System;
using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectMeshInputBundle(
    PackageResourceId ResourceId,
    string FormattedKey,
    CanonicalMesh CanonicalMesh,
    IReadOnlyList<byte>? RawPayload = null,
    uint? AssociatedLodIndex = null,
    uint? AssociatedGroupIndex = null,
    string? MaterialReferenceKey = null,
    IReadOnlyList<ConversionIssue>? Issues = null
);
