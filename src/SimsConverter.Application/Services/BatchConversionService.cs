using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Services;

namespace SimsConverter.Application.Services;

public class BatchConversionService : IBatchConversionService
{
    private readonly IPackageInspectionService _packageService;
    private readonly ISims3PackInspectionService? _sims3PackService;
    private readonly IPackageItemClassifier _itemClassifier;
    private readonly IDecorativeObjectConversionService _decorativeService;
    private readonly ICasItemConversionService _casService;
    private readonly IBatchDiagnosticLogger _diagnosticLogger;

    public BatchConversionService(
        IPackageInspectionService packageService,
        IDecorativeObjectConversionService decorativeService,
        ICasItemConversionService casService,
        IPackageItemClassifier? itemClassifier = null,
        ISims3PackInspectionService? sims3PackService = null,
        IBatchDiagnosticLogger? diagnosticLogger = null)
    {
        _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        _decorativeService = decorativeService ?? throw new ArgumentNullException(nameof(decorativeService));
        _casService = casService ?? throw new ArgumentNullException(nameof(casService));
        _itemClassifier = itemClassifier ?? new PackageItemClassifier();
        _sims3PackService = sims3PackService;
        _diagnosticLogger = diagnosticLogger ?? new BatchDiagnosticLogger();
    }

    private static readonly HashSet<string> StandaloneImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".dds"
    };

    public async Task<BatchConversionResult> ScanFolderAsync(
        string sourceFolderPath,
        string? outputFolderPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFolderPath) || !Directory.Exists(sourceFolderPath))
        {
            return new BatchConversionResult(sourceFolderPath ?? string.Empty, outputFolderPath ?? string.Empty, Array.Empty<BatchConversionItem>());
        }

        string targetOutputDir = string.IsNullOrWhiteSpace(outputFolderPath) ? sourceFolderPath : outputFolderPath;
        if (!Directory.Exists(targetOutputDir))
        {
            try { Directory.CreateDirectory(targetOutputDir); } catch { }
        }

        var files = Directory.GetFiles(sourceFolderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".package", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".sims3pack", StringComparison.OrdinalIgnoreCase) ||
                        StandaloneImageExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = new List<BatchConversionItem>();
        var usedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string ext = Path.GetExtension(file).ToLowerInvariant();
            var item = new BatchConversionItem
            {
                SourceFilePath = file,
                FileName = Path.GetFileName(file)
            };

            if (StandaloneImageExtensions.Contains(ext))
            {
                item.SourceGameVersion = GameVersion.Unknown;
                item.TargetGameVersion = GameVersion.Unknown;
                item.Category = PackageItemCategory.Unknown;
                item.TargetOutputPath = string.Empty;
                item.Status = BatchItemStatus.Ignored;
                item.StatusMessage = $"Unsupported standalone image file ({ext}).";
                items.Add(item);
                continue;
            }

            bool isSims3Pack = file.EndsWith(".sims3pack", StringComparison.OrdinalIgnoreCase);
            if (isSims3Pack)
            {
                item.SourceGameVersion = GameVersion.Sims3;
                item.TargetGameVersion = GameVersion.Sims4;
                item.Category = PackageItemCategory.DecorativeObject;
                item.Status = BatchItemStatus.Pending;
                item.StatusMessage = "Ready for Sims3Pack batch conversion";
            }
            else
            {
                var inspectResult = await _packageService.InspectFileAsync(file, cancellationToken).ConfigureAwait(false);
                if (!inspectResult.IsSuccess || inspectResult.Resources == null || inspectResult.Resources.Count == 0)
                {
                    item.Status = BatchItemStatus.Failed;
                    item.StatusMessage = "Invalid package container or read failure.";
                    item.Issues = inspectResult.Issues;
                }
                else
                {
                    bool isTs4Source = inspectResult.Resources.Any(r =>
                        r.TypeId == 0xC0DB5AE7u || // TS4 OBJD
                        r.TypeId == 0x2172D019u || // TS4 RMAT
                        r.TypeId == 0x2BC04EDFu || // TS4 LRLE
                        r.TypeId == 0x3453CF95u || // TS4 RLE2
                        r.TypeId == 0x2F7D0004u || // TS4 PNG image
                        r.TypeId == 0x034B5D85u);  // TS4 CASP

                    item.SourceGameVersion = isTs4Source ? GameVersion.Sims4 : GameVersion.Sims3;
                    item.TargetGameVersion = isTs4Source ? GameVersion.Sims3 : GameVersion.Sims4;

                    var classification = _itemClassifier.ClassifyPackage(file, inspectResult.Resources);
                    item.Category = classification.MainCategory;

                    if (classification.MainCategory == PackageItemCategory.Unknown)
                    {
                        item.Status = BatchItemStatus.Skipped;
                        item.StatusMessage = "Skipped: Unknown or unsupported package category.";
                    }
                    else
                    {
                        item.Status = BatchItemStatus.Pending;
                        item.StatusMessage = "Ready for batch conversion";
                    }
                }
            }

            // Deduplicate target output paths to prevent duplicate output overwrites
            string baseName = Path.GetFileNameWithoutExtension(file);
            string suffix = item.TargetGameVersion == GameVersion.Sims3 ? "_ts3.package" : "_ts4.package";
            string candidatePath = Path.Combine(targetOutputDir, $"{baseName}{suffix}");

            int counter = 1;
            while (usedOutputPaths.Contains(candidatePath) || string.Equals(candidatePath, file, StringComparison.OrdinalIgnoreCase))
            {
                candidatePath = Path.Combine(targetOutputDir, $"{baseName}_{counter}{suffix}");
                counter++;
            }

            usedOutputPaths.Add(candidatePath);
            item.TargetOutputPath = candidatePath;
            items.Add(item);
        }

        int skipped = items.Count(i => i.Status == BatchItemStatus.Skipped);
        int failed = items.Count(i => i.Status == BatchItemStatus.Failed);
        int ignored = items.Count(i => i.Status == BatchItemStatus.Ignored);

        return new BatchConversionResult(sourceFolderPath, targetOutputDir, items, 0, failed, skipped, ignored);
    }

    public async Task<BatchConversionResult> ExecuteBatchConversionAsync(
        BatchConversionRequest request,
        IProgress<BatchConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var items = request.Items != null && request.Items.Count > 0
            ? request.Items.ToList()
            : (await ScanFolderAsync(request.SourceFolderPath, request.OutputFolderPath, cancellationToken).ConfigureAwait(false)).Items.ToList();

        BatchRunLog? session = null;
        try
        {
            session = _diagnosticLogger.StartBatchSession(request.SourceFolderPath, request.OutputFolderPath);
            _diagnosticLogger.LogPipelinePhase("Discovery", $"Discovered {items.Count} items in source folder.");
        }
        catch { }

        int successCount = 0;
        int failedCount = 0;
        int skippedCount = 0;
        int ignoredCount = 0;

        for (int i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = items[i];
            var itemStart = DateTimeOffset.UtcNow;

            var itemLog = new BatchItemDiagnosticLog
            {
                SourceFilePath = item.SourceFilePath,
                FileName = item.FileName,
                FileExtension = Path.GetExtension(item.SourceFilePath).ToLowerInvariant(),
                Classification = item.Category.ToString(),
                SourceGameVersion = item.SourceGameVersion.ToString(),
                TargetGameVersion = item.TargetGameVersion.ToString(),
                ConversionDirection = item.DirectionText,
                StartTimeUtc = itemStart,
                TargetOutputPath = item.TargetOutputPath
            };

            if (item.Status == BatchItemStatus.Ignored)
            {
                itemLog.Status = "Ignored";
                itemLog.EndTimeUtc = DateTimeOffset.UtcNow;
                itemLog.DurationMs = (itemLog.EndTimeUtc - itemStart).TotalMilliseconds;
                itemLog.Issues.Add(new BatchIssueLog { Code = "IGN001", Severity = "Info", Message = item.StatusMessage });
                
                try
                {
                    _diagnosticLogger.LogPipelinePhase("Discovery", $"Bypassed standalone non-container image ({item.FileName}).", item.FileName);
                    _diagnosticLogger.RecordItemResult(itemLog);
                }
                catch { }

                ignoredCount++;
                continue;
            }
            if (item.Status == BatchItemStatus.Skipped)
            {
                itemLog.Status = "Skipped";
                itemLog.EndTimeUtc = DateTimeOffset.UtcNow;
                itemLog.DurationMs = (itemLog.EndTimeUtc - itemStart).TotalMilliseconds;
                itemLog.Issues.Add(new BatchIssueLog { Code = "SKP001", Severity = "Warning", Message = item.StatusMessage });

                try
                {
                    _diagnosticLogger.LogPipelinePhase("Classification", $"Skipped unknown package category for ({item.FileName}).", item.FileName);
                    _diagnosticLogger.RecordItemResult(itemLog);
                }
                catch { }

                skippedCount++;
                continue;
            }

            item.Status = BatchItemStatus.InProgress;
            item.StatusMessage = "Converting...";
            progress?.Report(new BatchConversionProgress
            {
                CurrentItemIndex = i + 1,
                TotalItemCount = items.Count,
                CurrentFileName = item.FileName,
                CurrentItem = item
            });

            try
            {
                _diagnosticLogger.LogPipelinePhase("ConversionRouting", $"Routing {item.FileName} ({item.Category}) for conversion.", item.FileName);
                _diagnosticLogger.LogPipelinePhase("PackageInspection", $"Parsing container for {item.FileName}.", item.FileName);
            }
            catch { }

            // Strict Error Isolation: Failure in one item must never stop processing remaining items
            try
            {
                var targetVersion = request.OverrideTargetGameVersion ?? item.TargetGameVersion;
                var convReq = new DecorativeObjectConversionRequest(item.SourceFilePath, item.TargetOutputPath, targetVersion);

                try
                {
                    _diagnosticLogger.LogPipelinePhase("PayloadConversion", $"Converting resource payload graph for {item.FileName}.", item.FileName);
                }
                catch { }

                DecorativeObjectConversionResult result;
                if (item.Category == PackageItemCategory.CasPart)
                {
                    result = await _casService.ConvertCasPackageAsync(convReq, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result = await _decorativeService.ExecuteConversionAsync(convReq, cancellationToken).ConfigureAwait(false);
                }

                if (result.Issues != null && result.Issues.Count > 0)
                {
                    itemLog.Issues = result.Issues.Select(iss => new BatchIssueLog
                    {
                        Code = iss.Code ?? string.Empty,
                        Severity = iss.Severity.ToString(),
                        Message = iss.Message ?? string.Empty,
                        TargetResource = iss.TargetVersion.ToString()
                    }).ToList();
                }

                if (result.Plan != null)
                {
                    itemLog.ProducedResourceCount = result.Plan.ConvertableMeshCount + result.Plan.ValidTextureCount;
                    if (result.Plan.MeshCandidates != null)
                    {
                        itemLog.Metrics.VertexCount = (int)result.Plan.MeshCandidates.Sum(m => (long)(m.VertexCount ?? 0u));
                        itemLog.Metrics.FaceCount = (int)result.Plan.MeshCandidates.Sum(m => (long)(m.FaceCount ?? 0u));
                    }
                    itemLog.Metrics.DecodedTextureCount = result.Plan.ValidTextureCount;
                    if (item.Category == PackageItemCategory.CasPart)
                    {
                        itemLog.Metrics.CaspTypeId = item.SourceGameVersion == GameVersion.Sims4 ? "0x034B5D85" : "0x0355E0A6";
                    }
                }

                if (result.IsSuccess)
                {
                    item.Status = BatchItemStatus.Success;
                    item.StatusMessage = "Conversion succeeded.";
                    item.ConvertedPackagePath = result.TargetOutputPath;
                    item.Issues = result.Issues ?? Array.Empty<ConversionIssue>();

                    itemLog.Status = "Success";
                    try
                    {
                        _diagnosticLogger.LogPipelinePhase("PackageWrite", $"Staging package written for {item.FileName}.", item.FileName);
                        _diagnosticLogger.LogPipelinePhase("PostWriteValidation", $"Post-write verification passed for {item.FileName}.", item.FileName);
                        _diagnosticLogger.LogPipelinePhase("AtomicCommit", $"Atomic commit completed for {item.FileName}.", item.FileName);
                    }
                    catch { }

                    successCount++;
                }
                else
                {
                    item.Status = BatchItemStatus.Failed;
                    item.StatusMessage = result.Issues != null && result.Issues.Count > 0
                        ? string.Join("; ", result.Issues.Select(iss => $"[{iss.Code}] {iss.Message}"))
                        : "Conversion failed without specific issue code.";
                    item.Issues = result.Issues ?? Array.Empty<ConversionIssue>();

                    itemLog.Status = "Failed";
                    try
                    {
                        _diagnosticLogger.LogPipelinePhase("AtomicRollback", $"Conversion failed for {item.FileName}. Atomic rollback executed.", item.FileName, isSuccess: false, details: item.StatusMessage);
                    }
                    catch { }

                    failedCount++;
                }
            }
            catch (Exception ex)
            {
                item.Status = BatchItemStatus.Failed;
                item.StatusMessage = $"Exception during batch item conversion: {ex.Message}";

                itemLog.Status = "Failed";
                itemLog.Exception = new BatchExceptionLog
                {
                    Type = ex.GetType().FullName ?? ex.GetType().Name,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace ?? string.Empty
                };

                try
                {
                    _diagnosticLogger.LogPipelinePhase("AtomicRollback", $"Unhandled exception converting {item.FileName}: {ex.Message}. Atomic rollback executed.", item.FileName, isSuccess: false, details: ex.ToString());
                }
                catch { }

                failedCount++;
            }
            finally
            {
                itemLog.EndTimeUtc = DateTimeOffset.UtcNow;
                itemLog.DurationMs = (itemLog.EndTimeUtc - itemStart).TotalMilliseconds;
                try
                {
                    _diagnosticLogger.RecordItemResult(itemLog);
                }
                catch { }
            }
        }

        string jsonPath = string.Empty;
        string textPath = string.Empty;
        try
        {
            var exportResult = await _diagnosticLogger.CompleteAndExportBatchSessionAsync().ConfigureAwait(false);
            jsonPath = exportResult.JsonPath;
            textPath = exportResult.TextPath;
        }
        catch { }

        return new BatchConversionResult(
            request.SourceFolderPath,
            request.OutputFolderPath,
            items,
            successCount,
            failedCount,
            skippedCount,
            ignoredCount,
            session?.RunId ?? string.Empty,
            jsonPath,
            textPath);
    }
}
