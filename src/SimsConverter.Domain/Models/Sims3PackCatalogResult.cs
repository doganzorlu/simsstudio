using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record Sims3PackCatalogResult(
    bool IsSuccess,
    IReadOnlyList<Sims3PackCatalogEntry> Entries,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static Sims3PackCatalogResult Failure(string issueCode, string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Error);
        return new Sims3PackCatalogResult(
            false,
            Array.Empty<Sims3PackCatalogEntry>(),
            new[] { issue }
        );
    }
}
