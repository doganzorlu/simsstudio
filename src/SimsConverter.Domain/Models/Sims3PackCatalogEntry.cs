using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record Sims3PackCatalogEntry(
    int EntryIndex,
    Sims3PackPayloadKind Kind,
    long DataOffset,
    long? EstimatedSizeBytes,
    string? DisplayName,
    IReadOnlyList<ConversionIssue> Issues
);
