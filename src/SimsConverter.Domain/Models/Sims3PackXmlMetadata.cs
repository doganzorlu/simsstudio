using System;
using System.Collections.Generic;

namespace SimsConverter.Domain.Models;

public record Sims3PackXmlMetadata(
    string RootElementName,
    string DeclaredEncoding,
    long RawXmlSizeBytes,
    int EmbeddedFileCount,
    IReadOnlyList<string> EmbeddedFileNames,
    string? Title = null,
    string? AssetId = null,
    string? AssetType = null,
    string? Description = null,
    IReadOnlyList<ConversionIssue>? Issues = null
)
{
    public IReadOnlyList<ConversionIssue> Issues { get; init; } = Issues ?? Array.Empty<ConversionIssue>();
}
