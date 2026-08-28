using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record Sims3PackParseResult(
    bool IsSuccess,
    Sims3PackXmlMetadata? Metadata,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public static Sims3PackParseResult Failure(string issueCode, string issueMessage)
    {
        var issue = new ConversionIssue(issueCode, issueMessage, ConversionIssueSeverity.Error);
        return new Sims3PackParseResult(
            false,
            null,
            new[] { issue }
        );
    }
}
