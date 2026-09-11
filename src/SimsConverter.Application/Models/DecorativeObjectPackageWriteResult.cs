using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectPackageWriteResult(
    bool IsSuccess,
    string TargetOutputPath,
    int ResourceCount,
    long BytesWritten,
    IReadOnlyList<ConversionIssue> Issues
);
