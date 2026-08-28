namespace SimsConverter.Application.Models;

public record SingleResourceExportRequest(
    string SourcePackagePath,
    PackageResourceRow Resource,
    string DestinationDirectory,
    bool AllowOverwrite = false
);
