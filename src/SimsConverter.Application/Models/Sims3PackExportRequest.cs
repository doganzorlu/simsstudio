namespace SimsConverter.Application.Models;

public record Sims3PackExportRequest(
    string SourceSims3PackPath,
    Sims3PackPayloadRow SelectedRow,
    string OutputDirectory,
    string? CustomFileName = null,
    bool AllowOverwrite = false
);
