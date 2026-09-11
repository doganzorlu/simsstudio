using System.Collections.Generic;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Models;

public record DbpfPackageWriteResult(
    bool IsSuccess,
    string TargetOutputPath,
    int ResourceCount,
    long BytesWritten,
    IReadOnlyList<ConversionIssue> Issues
);
