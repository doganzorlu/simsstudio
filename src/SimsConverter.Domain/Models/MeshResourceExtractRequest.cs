namespace SimsConverter.Domain.Models;

public record MeshResourceExtractRequest(
    string SourcePackagePath,
    PackageResourceEntry Entry,
    MeshResourceClassification Classification,
    string OutputDirectory,
    string? CustomFileName = null,
    bool AllowOverwrite = false
);
