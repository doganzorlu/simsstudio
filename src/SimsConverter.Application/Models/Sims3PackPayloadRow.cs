using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record Sims3PackPayloadRow(
    int EntryIndex,
    string Kind,
    long DataOffset,
    string DataOffsetHex,
    long? EstimatedSizeBytes,
    string EstimatedSizeFormatted,
    string DisplayName,
    bool CanExport,
    IReadOnlyList<ConversionIssue> Issues
);
