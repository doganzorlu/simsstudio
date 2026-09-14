using System;
using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public enum BatchItemStatus
{
    Pending,
    InProgress,
    Success,
    Failed,
    Skipped,
    Ignored
}

public class BatchConversionItem
{
    public string SourceFilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string TargetOutputPath { get; set; } = string.Empty;
    public PackageItemCategory Category { get; set; } = PackageItemCategory.Unknown;
    public GameVersion SourceGameVersion { get; set; } = GameVersion.Sims3;
    public GameVersion TargetGameVersion { get; set; } = GameVersion.Sims4;
    public BatchItemStatus Status { get; set; } = BatchItemStatus.Pending;
    public string StatusMessage { get; set; } = "Pending batch conversion";
    public string? ConvertedPackagePath { get; set; }
    public IReadOnlyList<ConversionIssue> Issues { get; set; } = Array.Empty<ConversionIssue>();

    public string DirectionText => (SourceGameVersion == GameVersion.Unknown || TargetGameVersion == GameVersion.Unknown) ? "N/A" : $"{SourceGameVersion} -> {TargetGameVersion}";
}

public class BatchConversionRequest
{
    public string SourceFolderPath { get; set; } = string.Empty;
    public string OutputFolderPath { get; set; } = string.Empty;
    public IReadOnlyList<BatchConversionItem>? Items { get; set; }
    public GameVersion? OverrideTargetGameVersion { get; set; }

    public BatchConversionRequest(string sourceFolderPath, string? outputFolderPath = null, IReadOnlyList<BatchConversionItem>? items = null)
    {
        SourceFolderPath = sourceFolderPath ?? throw new ArgumentNullException(nameof(sourceFolderPath));
        OutputFolderPath = string.IsNullOrWhiteSpace(outputFolderPath) ? sourceFolderPath : outputFolderPath;
        Items = items;
    }
}

public class BatchConversionProgress
{
    public int CurrentItemIndex { get; set; }
    public int TotalItemCount { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public BatchConversionItem? CurrentItem { get; set; }
}

public class BatchConversionResult
{
    public string SourceFolderPath { get; set; }
    public string OutputFolderPath { get; set; }
    public int TotalCount => Items.Count;
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public int IgnoredCount { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string? LogFilePathJson { get; set; }
    public string? LogFilePathText { get; set; }
    public IReadOnlyList<BatchConversionItem> Items { get; set; }

    public BatchConversionResult(
        string sourceFolderPath,
        string outputFolderPath,
        IReadOnlyList<BatchConversionItem> items,
        int successCount = 0,
        int failedCount = 0,
        int skippedCount = 0,
        int ignoredCount = 0,
        string runId = "",
        string? logFilePathJson = null,
        string? logFilePathText = null)
    {
        SourceFolderPath = sourceFolderPath;
        OutputFolderPath = outputFolderPath;
        Items = items ?? Array.Empty<BatchConversionItem>();
        SuccessCount = successCount;
        FailedCount = failedCount;
        SkippedCount = skippedCount;
        IgnoredCount = ignoredCount;
        RunId = runId;
        LogFilePathJson = logFilePathJson;
        LogFilePathText = logFilePathText;
    }
}
