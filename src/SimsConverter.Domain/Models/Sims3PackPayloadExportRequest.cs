using System;

namespace SimsConverter.Domain.Models;

public record Sims3PackPayloadExportRequest(
    string SourceSims3PackPath,
    Sims3PackCatalogEntry CatalogEntry,
    string OutputFilePath,
    bool AllowOverwrite = false
);
