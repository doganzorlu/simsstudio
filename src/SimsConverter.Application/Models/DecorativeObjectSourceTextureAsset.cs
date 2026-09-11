using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectSourceTextureAsset(
    string FormattedKey,
    PackageResourceId ResourceId,
    TextureClassificationKind ClassificationKind,
    TextureMapKind MapKind,
    string FormatName,
    bool CanExtractRawPayload,
    IReadOnlyList<ConversionIssue> Issues,
    PackageResourceEntry? Entry = null,
    IReadOnlyList<byte>? RawPayload = null
);
