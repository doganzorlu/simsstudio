using System;
using System.Collections.Generic;

namespace SimsConverter.Application.Models;

public class BatchRunLog
{
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
    public double TotalDurationMs { get; set; }
    public string SourceFolderPath { get; set; } = string.Empty;
    public string OutputFolderPath { get; set; } = string.Empty;
    public BatchSummaryLog Summary { get; set; } = new();
    public List<BatchPipelinePhaseLog> PipelinePhases { get; set; } = new();
    public List<BatchItemDiagnosticLog> Files { get; set; } = new();
    public string LogFilePathJson { get; set; } = string.Empty;
    public string LogFilePathText { get; set; } = string.Empty;
}

public class BatchSummaryLog
{
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public int IgnoredCount { get; set; }
    public double TotalDurationMs { get; set; }
}

public class BatchItemDiagnosticLog
{
    public string SourceFilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string SourceGameVersion { get; set; } = string.Empty;
    public string TargetGameVersion { get; set; } = string.Empty;
    public string ConversionDirection { get; set; } = string.Empty;
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
    public double DurationMs { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TargetOutputPath { get; set; } = string.Empty;
    public int ProducedResourceCount { get; set; }
    public BatchItemMetricsLog Metrics { get; set; } = new();
    public List<BatchIssueLog> Issues { get; set; } = new();
    public BatchExceptionLog? Exception { get; set; }
}

public class BatchItemMetricsLog
{
    public int VertexCount { get; set; }
    public int FaceCount { get; set; }
    public int DecodedTextureCount { get; set; }
    public string CaspTypeId { get; set; } = string.Empty;
    public Dictionary<string, string> AdditionalMetrics { get; set; } = new();
}

public class BatchIssueLog
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TargetResource { get; set; } = string.Empty;
}

public class BatchExceptionLog
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
}

public class BatchPipelinePhaseLog
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess { get; set; } = true;
    public string? Details { get; set; }
}
