using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Application.Services;
using Xunit;

namespace SimsConverter.Application.Tests;

public class BatchDiagnosticLoggerTests
{
    private static string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "diag_log_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task CompleteAndExportBatchSessionAsync_ExportsValidJsonAndTextLogFiles()
    {
        string tempDir = CreateTempDir();
        try
        {
            var logger = new BatchDiagnosticLogger();
            var session = logger.StartBatchSession("/source/folder", "/output/folder", "testrun123");

            session.RunId.Should().Be("testrun123");

            logger.LogPipelinePhase("Discovery", "Discovered 3 files.");
            logger.LogPipelinePhase("Classification", "Classified files.");

            // 1. Success Item
            logger.RecordItemResult(new BatchItemDiagnosticLog
            {
                SourceFilePath = "/source/item1.package",
                FileName = "item1.package",
                FileExtension = ".package",
                Classification = "CasPart",
                SourceGameVersion = "Sims4",
                TargetGameVersion = "Sims3",
                ConversionDirection = "Sims4 -> Sims3",
                Status = "Success",
                TargetOutputPath = "/output/item1_ts3.package",
                ProducedResourceCount = 12,
                Metrics = new BatchItemMetricsLog { VertexCount = 1000, FaceCount = 1500, DecodedTextureCount = 4, CaspTypeId = "0x034B5D85" }
            });

            // 2. Failed Item
            logger.RecordItemResult(new BatchItemDiagnosticLog
            {
                SourceFilePath = "/source/item2.package",
                FileName = "item2.package",
                FileExtension = ".package",
                Classification = "DecorativeObject",
                SourceGameVersion = "Sims3",
                TargetGameVersion = "Sims4",
                ConversionDirection = "Sims3 -> Sims4",
                Status = "Failed",
                TargetOutputPath = "/output/item2_ts4.package",
                ProducedResourceCount = 0,
                Issues = new System.Collections.Generic.List<BatchIssueLog>
                {
                    new BatchIssueLog { Code = "ERR001", Severity = "Error", Message = "Corrupt DBPF container." }
                }
            });

            // 3. Ignored Item
            logger.RecordItemResult(new BatchItemDiagnosticLog
            {
                SourceFilePath = "/source/preview.png",
                FileName = "preview.png",
                FileExtension = ".png",
                Classification = "Unknown",
                SourceGameVersion = "Unknown",
                TargetGameVersion = "Unknown",
                ConversionDirection = "N/A",
                Status = "Ignored",
                TargetOutputPath = string.Empty
            });

            // Act
            var (jsonPath, textPath) = await logger.CompleteAndExportBatchSessionAsync(tempDir);

            // Assert
            File.Exists(jsonPath).Should().BeTrue("JSON diagnostic log file must be created.");
            File.Exists(textPath).Should().BeTrue("Text diagnostic log file must be created.");

            // JSON Parseability Test
            string jsonText = await File.ReadAllTextAsync(jsonPath);
            var deserializedLog = JsonSerializer.Deserialize<BatchRunLog>(jsonText);
            deserializedLog.Should().NotBeNull();
            deserializedLog!.RunId.Should().Be("testrun123");
            deserializedLog.Summary.TotalFiles.Should().Be(3);
            deserializedLog.Summary.SuccessCount.Should().Be(1);
            deserializedLog.Summary.FailedCount.Should().Be(1);
            deserializedLog.Summary.IgnoredCount.Should().Be(1);
            deserializedLog.Files.Should().HaveCount(3);

            // Text Log content verification
            string textLogContent = await File.ReadAllTextAsync(textPath);
            textLogContent.Should().Contain("SIMSSTUDIO BATCH CONVERSION DIAGNOSTIC LOG");
            textLogContent.Should().Contain("Run ID:           testrun123");
            textLogContent.Should().Contain("Total Files:      3");
            textLogContent.Should().Contain("Successful:       1");
            textLogContent.Should().Contain("item1.package");
            textLogContent.Should().Contain("Vertices=1000, Faces=1500");
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task LogExceptionAndRollback_RecordsFullStackTraceAndRollbackPhase()
    {
        string tempDir = CreateTempDir();
        try
        {
            var logger = new BatchDiagnosticLogger();
            logger.StartBatchSession("/source", "/target");

            logger.LogPipelinePhase("AtomicRollback", "Conversion failed due to unhandled exception. Rolled back staging file.", "faulty.package", isSuccess: false, details: "NullReferenceException");

            var ex = new InvalidOperationException("Simulated payload corruption during write");

            logger.RecordItemResult(new BatchItemDiagnosticLog
            {
                SourceFilePath = "/source/faulty.package",
                FileName = "faulty.package",
                FileExtension = ".package",
                Classification = "DecorativeObject",
                Status = "Failed",
                Exception = new BatchExceptionLog
                {
                    Type = ex.GetType().FullName!,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace ?? "   at SimsConverter.Application.Services.BatchConversionService.ExecuteBatchConversionAsync"
                }
            });

            var (jsonPath, textPath) = await logger.CompleteAndExportBatchSessionAsync(tempDir);

            string textLog = await File.ReadAllTextAsync(textPath);
            textLog.Should().Contain("[AtomicRollback] [faulty.package] (FAIL)");
            textLog.Should().Contain("InvalidOperationException");
            textLog.Should().Contain("Simulated payload corruption during write");
        }
        finally
        {
            if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private class ThrowingFaultyLogger : IBatchDiagnosticLogger
    {
        public BatchRunLog? CurrentSession => null;

        public BatchRunLog StartBatchSession(string sourceFolderPath, string outputFolderPath, string? customRunId = null)
        {
            throw new InvalidOperationException("Simulated logger failure!");
        }

        public void LogPipelinePhase(string phase, string message, string? fileName = null, bool isSuccess = true, string? details = null)
        {
            throw new InvalidOperationException("Simulated logger phase failure!");
        }

        public void RecordItemResult(BatchItemDiagnosticLog itemLog)
        {
            throw new InvalidOperationException("Simulated logger record failure!");
        }

        public Task<(string JsonPath, string TextPath)> CompleteAndExportBatchSessionAsync(string? baseDirectory = null)
        {
            throw new InvalidOperationException("Simulated logger export failure!");
        }
    }

    [Fact]
    public void PathSanitization_SanitizesSensitiveQueryParameters()
    {
        string inputPath = "/Users/test/data/package.package?token=SECRET123&password=PASSWORD123";
        string sanitized = BatchDiagnosticLogger.SanitizePath(inputPath);

        sanitized.Should().Be("/Users/test/data/package.package?[REDACTED_QUERY_PARAMS]");
    }
}
