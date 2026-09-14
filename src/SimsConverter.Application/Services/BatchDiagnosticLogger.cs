using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Services;

public class BatchDiagnosticLogger : IBatchDiagnosticLogger
{
    private readonly object _syncLock = new();
    private BatchRunLog? _currentSession;
    private DateTimeOffset _startTime;

    public BatchRunLog? CurrentSession => _currentSession;

    public BatchRunLog StartBatchSession(string sourceFolderPath, string outputFolderPath, string? customRunId = null)
    {
        lock (_syncLock)
        {
            _startTime = DateTimeOffset.UtcNow;
            string runId = string.IsNullOrWhiteSpace(customRunId) ? Guid.NewGuid().ToString("N") : customRunId;

            _currentSession = new BatchRunLog
            {
                RunId = runId,
                StartTimeUtc = _startTime,
                SourceFolderPath = SanitizePath(sourceFolderPath),
                OutputFolderPath = SanitizePath(outputFolderPath),
                Summary = new BatchSummaryLog(),
                Files = new List<BatchItemDiagnosticLog>(),
                PipelinePhases = new List<BatchPipelinePhaseLog>()
            };

            return _currentSession;
        }
    }

    public void LogPipelinePhase(string phase, string message, string? fileName = null, bool isSuccess = true, string? details = null)
    {
        lock (_syncLock)
        {
            if (_currentSession == null) return;

            try
            {
                _currentSession.PipelinePhases.Add(new BatchPipelinePhaseLog
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Phase = phase,
                    FileName = fileName,
                    Message = message,
                    IsSuccess = isSuccess,
                    Details = details
                });
            }
            catch
            {
                // Requirement 8: Logging must never throw or disrupt execution
            }
        }
    }

    public void RecordItemResult(BatchItemDiagnosticLog itemLog)
    {
        if (itemLog == null) return;

        lock (_syncLock)
        {
            if (_currentSession == null) return;

            try
            {
                itemLog.SourceFilePath = SanitizePath(itemLog.SourceFilePath);
                if (!string.IsNullOrWhiteSpace(itemLog.TargetOutputPath))
                {
                    itemLog.TargetOutputPath = SanitizePath(itemLog.TargetOutputPath);
                }

                _currentSession.Files.Add(itemLog);
            }
            catch
            {
                // Requirement 8: Logging must never throw or disrupt execution
            }
        }
    }

    public async Task<(string JsonPath, string TextPath)> CompleteAndExportBatchSessionAsync(string? baseDirectory = null)
    {
        lock (_syncLock)
        {
            if (_currentSession == null)
            {
                return (string.Empty, string.Empty);
            }
        }

        try
        {
            DateTimeOffset endTime = DateTimeOffset.UtcNow;
            _currentSession.EndTimeUtc = endTime;
            _currentSession.TotalDurationMs = (endTime - _startTime).TotalMilliseconds;

            _currentSession.Summary = new BatchSummaryLog
            {
                TotalFiles = _currentSession.Files.Count,
                SuccessCount = _currentSession.Files.Count(f => string.Equals(f.Status, "Success", StringComparison.OrdinalIgnoreCase)),
                FailedCount = _currentSession.Files.Count(f => string.Equals(f.Status, "Failed", StringComparison.OrdinalIgnoreCase)),
                SkippedCount = _currentSession.Files.Count(f => string.Equals(f.Status, "Skipped", StringComparison.OrdinalIgnoreCase)),
                IgnoredCount = _currentSession.Files.Count(f => string.Equals(f.Status, "Ignored", StringComparison.OrdinalIgnoreCase)),
                TotalDurationMs = _currentSession.TotalDurationMs
            };

            string logDir = ResolveLogDirectory(baseDirectory);
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            string timeStampStr = _startTime.ToString("yyyyMMdd-HHmmss");
            string fileNameBase = $"batch-conversion-{timeStampStr}-{_currentSession.RunId}";

            string jsonPath = Path.Combine(logDir, $"{fileNameBase}.json");
            string textPath = Path.Combine(logDir, $"{fileNameBase}.log");

            _currentSession.LogFilePathJson = jsonPath;
            _currentSession.LogFilePathText = textPath;

            // 1. Export JSON format
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            string jsonContent = JsonSerializer.Serialize(_currentSession, jsonOptions);
            await File.WriteAllTextAsync(jsonPath, jsonContent, Encoding.UTF8).ConfigureAwait(false);

            // 2. Export Text log format
            string textContent = GenerateTextLog(_currentSession);
            await File.WriteAllTextAsync(textPath, textContent, Encoding.UTF8).ConfigureAwait(false);

            return (jsonPath, textPath);
        }
        catch (Exception ex)
        {
            // Requirement 8: Failure to write log files MUST NEVER fail or crash conversion execution
            string fallbackError = $"[BatchDiagnosticLogger Error] Failed to export log files: {ex.Message}";
            return (fallbackError, fallbackError);
        }
    }

    private static string ResolveLogDirectory(string? baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            return Path.Combine(baseDirectory, "artifacts", "logs");
        }

        string currentDir = AppContext.BaseDirectory;
        string? solutionRoot = FindSolutionOrProjectRoot(currentDir);
        string rootDir = solutionRoot ?? Directory.GetCurrentDirectory();

        return Path.Combine(rootDir, "artifacts", "logs");
    }

    private static string? FindSolutionOrProjectRoot(string startDir)
    {
        string? dir = startDir;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "SimsConverter.sln")) || Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    public static string SanitizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        // Normalize slash format for cross-platform log consistency
        string normalized = path.Replace('\\', '/');

        // Sensitive Token Redaction Policy:
        // Sensitive parameters or embedded auth tokens in URLs/paths are redacted
        if (normalized.Contains("password=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("secret=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("token=", StringComparison.OrdinalIgnoreCase))
        {
            int idx = normalized.IndexOf('?');
            if (idx >= 0)
            {
                return normalized.Substring(0, idx) + "?[REDACTED_QUERY_PARAMS]";
            }
        }

        return normalized;
    }

    private static string GenerateTextLog(BatchRunLog log)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("SIMSSTUDIO BATCH CONVERSION DIAGNOSTIC LOG");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Run ID:           {log.RunId}");
        sb.AppendLine($"Start Time (UTC): {log.StartTimeUtc:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"End Time (UTC):   {log.EndTimeUtc:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Total Duration:   {log.TotalDurationMs:F2} ms");
        sb.AppendLine($"Source Folder:    {log.SourceFolderPath}");
        sb.AppendLine($"Target Folder:    {log.OutputFolderPath}");
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("BATCH CONVERSION SUMMARY");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"Total Files:      {log.Summary.TotalFiles}");
        sb.AppendLine($"Successful:       {log.Summary.SuccessCount}");
        sb.AppendLine($"Failed:           {log.Summary.FailedCount}");
        sb.AppendLine($"Skipped:          {log.Summary.SkippedCount}");
        sb.AppendLine($"Ignored:          {log.Summary.IgnoredCount}");
        sb.AppendLine($"Total Duration:   {log.Summary.TotalDurationMs:F2} ms");
        sb.AppendLine();

        if (log.PipelinePhases.Count > 0)
        {
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("PIPELINE PHASES LOG");
            sb.AppendLine("--------------------------------------------------------------------------------");
            foreach (var phase in log.PipelinePhases)
            {
                string statusTag = phase.IsSuccess ? "OK" : "FAIL";
                string fileStr = string.IsNullOrWhiteSpace(phase.FileName) ? "" : $" [{phase.FileName}]";
                sb.AppendLine($"[{phase.TimestampUtc:HH:mm:ss.fff}] [{phase.Phase}]{fileStr} ({statusTag}) {phase.Message}");
                if (!string.IsNullOrWhiteSpace(phase.Details))
                {
                    sb.AppendLine($"   Details: {phase.Details}");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("FILE DIAGNOSTICS DETAILED BREAKDOWN");
        sb.AppendLine("--------------------------------------------------------------------------------");
        for (int i = 0; i < log.Files.Count; i++)
        {
            var f = log.Files[i];
            sb.AppendLine($"[{i + 1}/{log.Files.Count}] {f.FileName}");
            sb.AppendLine($"  Source Path:      {f.SourceFilePath}");
            sb.AppendLine($"  Extension:        {f.FileExtension}");
            sb.AppendLine($"  Classification:   {f.Classification}");
            sb.AppendLine($"  Direction:        {f.ConversionDirection}");
            sb.AppendLine($"  Status:           {f.Status}");
            sb.AppendLine($"  Duration:         {f.DurationMs:F2} ms");
            sb.AppendLine($"  Target Output:    {(string.IsNullOrWhiteSpace(f.TargetOutputPath) ? "N/A" : f.TargetOutputPath)}");
            sb.AppendLine($"  Resource Count:   {f.ProducedResourceCount}");

            if (f.Metrics != null)
            {
                sb.AppendLine($"  Metrics:          Vertices={f.Metrics.VertexCount}, Faces={f.Metrics.FaceCount}, Textures={f.Metrics.DecodedTextureCount}, CASP TypeId={(string.IsNullOrWhiteSpace(f.Metrics.CaspTypeId) ? "N/A" : f.Metrics.CaspTypeId)}");
            }

            if (f.Issues != null && f.Issues.Count > 0)
            {
                sb.AppendLine($"  Diagnostic Issues ({f.Issues.Count}):");
                foreach (var issue in f.Issues)
                {
                    sb.AppendLine($"    - [{issue.Severity}] [{issue.Code}] {issue.Message}");
                }
            }

            if (f.Exception != null)
            {
                sb.AppendLine("  Exception Breakdown:");
                sb.AppendLine($"    Type:    {f.Exception.Type}");
                sb.AppendLine($"    Message: {f.Exception.Message}");
                if (!string.IsNullOrWhiteSpace(f.Exception.StackTrace))
                {
                    sb.AppendLine("    StackTrace:");
                    foreach (var line in f.Exception.StackTrace.Split('\n'))
                    {
                        sb.AppendLine($"      {line.TrimEnd()}");
                    }
                }
            }
            sb.AppendLine();
        }
        sb.AppendLine("================================================================================");

        return sb.ToString();
    }
}
