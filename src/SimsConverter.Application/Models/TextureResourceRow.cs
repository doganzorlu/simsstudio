using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record TextureResourceRow(
    string FormattedKey,
    string TypeHex,
    string GroupHex,
    string InstanceHex,
    string FormatName,
    TextureMapKind MapKind,
    GameVersion DetectedGameVersion,
    TextureClassificationKind ClassificationKind,
    bool CanExtractRawPayload,
    bool CanParseDdsHeader,
    IReadOnlyList<ConversionIssue> Issues,
    PackageResourceEntry Entry,
    TextureResourceClassification Classification
);
