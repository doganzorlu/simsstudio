namespace SimsConverter.Domain.Models;

public record PackageResourceExportRequest(
    string SourcePackagePath,
    long Offset,
    uint CompressedSize,
    string OutputFilePath,
    bool AllowOverwrite = false
);
