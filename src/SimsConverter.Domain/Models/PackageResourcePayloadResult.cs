using System.Collections.Generic;

namespace SimsConverter.Domain.Models;

public record PackageResourcePayloadResult(
    bool IsSuccess,
    byte[]? Payload,
    IReadOnlyList<ConversionIssue> Issues
);
