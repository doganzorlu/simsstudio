using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record DbpfParseResult(
    bool IsSuccess,
    DbpfHeader? Header,
    IReadOnlyList<PackageResourceEntry> Entries,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static DbpfParseResult Failure(string code, string message)
    {
        var issue = new ConversionIssue(code, message, ConversionIssueSeverity.Error);
        return new DbpfParseResult(false, null, Array.Empty<PackageResourceEntry>(), new[] { issue });
    }
}
