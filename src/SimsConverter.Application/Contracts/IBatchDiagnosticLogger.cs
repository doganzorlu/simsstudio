using System;
using System.Threading.Tasks;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface IBatchDiagnosticLogger
{
    BatchRunLog StartBatchSession(string sourceFolderPath, string outputFolderPath, string? customRunId = null);

    void LogPipelinePhase(string phase, string message, string? fileName = null, bool isSuccess = true, string? details = null);

    void RecordItemResult(BatchItemDiagnosticLog itemLog);

    Task<(string JsonPath, string TextPath)> CompleteAndExportBatchSessionAsync(string? baseDirectory = null);

    BatchRunLog? CurrentSession { get; }
}
