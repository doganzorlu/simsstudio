using System.Collections.Generic;

namespace SimsConverter.Domain.Models;

public record MeshResourceExtractResult(
    bool IsSuccess,
    string SourcePackagePath,
    string TargetFilePath,
    long ExportedSizeBytes,
    IReadOnlyList<ConversionIssue> Issues
);
