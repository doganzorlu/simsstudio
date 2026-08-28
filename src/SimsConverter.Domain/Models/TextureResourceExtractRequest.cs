namespace SimsConverter.Domain.Models;

public record TextureResourceExtractRequest(
    string SourcePackagePath,
    PackageResourceEntry Entry,
    TextureResourceClassification Classification,
    string OutputDirectory,
    string? CustomFileName = null,
    bool AllowOverwrite = false
);
