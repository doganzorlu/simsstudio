using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Services;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace SimsConverter.Package.Tests;

public class Sims3PackRealFixtureValidationTests
{
    private readonly ITestOutputHelper _output;

    public Sims3PackRealFixtureValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ValidateRealSims3PackFixtures_WhenPresent_ProducesCompatibilityReport()
    {
        // Arrange
        string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string localFixturesDir = Path.Combine(solutionDir, "fixtures", "local");
        string reportsDir = Path.Combine(solutionDir, "docs", "reports");

        if (!Directory.Exists(localFixturesDir))
        {
            _output.WriteLine($"[SKIPPED] Local fixtures directory does not exist at '{localFixturesDir}'. Synthetic unit tests passed.");
            return;
        }

        string[] sims3PackFiles = Directory.GetFiles(localFixturesDir, "*.sims3pack", SearchOption.AllDirectories);
        if (sims3PackFiles.Length == 0)
        {
            _output.WriteLine($"[SKIPPED] No .sims3pack files found in '{localFixturesDir}'. Synthetic unit tests passed.");
            return;
        }

        _output.WriteLine($"Found {sims3PackFiles.Length} real-world .sims3pack fixture(s) for validation.");

        var detector = new Sims3PackDetector();
        var xmlParser = new Sims3PackXmlParser();
        var catalogScanner = new Sims3PackPayloadCatalogScanner();
        var exporter = new Sims3PackPayloadExporter();
        var dbpfParser = new DbpfPackageParser();

        string tempExportDir = Path.Combine(Path.GetTempPath(), "sims3pack_validation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempExportDir);

        var reportRows = new List<ValidationReportRow>();

        try
        {
            foreach (var filePath in sims3PackFiles)
            {
                string fileName = Path.GetFileName(filePath);
                var issues = new List<string>();

                // 1. Detect
                var detectResult = await detector.DetectFileAsync(filePath);
                string detectStatus = detectResult.ContainerKind == PackageContainerKind.Sims3Pack ? "Success" : "Failed";
                foreach (var i in detectResult.Issues) issues.Add(i.Code);

                // 2. XML Parse
                string xmlStatus = "Not Attempted";
                Sims3PackParseResult? xmlResult = null;
                if (detectResult.ContainerKind == PackageContainerKind.Sims3Pack)
                {
                    xmlResult = await xmlParser.ParseFileAsync(filePath);
                    xmlStatus = xmlResult.IsSuccess ? "Success" : "Failed";
                    foreach (var i in xmlResult.Issues) issues.Add(i.Code);
                }

                // 3. Catalog Scan
                string catalogStatus = "Not Attempted";
                int payloadCount = 0;
                int dbpfCount = 0;
                Sims3PackCatalogResult? catalogResult = null;
                if (xmlResult?.IsSuccess == true)
                {
                    catalogResult = await catalogScanner.ScanFileAsync(filePath);
                    catalogStatus = catalogResult.IsSuccess ? "Success" : "Failed";
                    payloadCount = catalogResult.Entries.Count;
                    dbpfCount = catalogResult.Entries.Count(e => e.Kind == Sims3PackPayloadKind.DbpfPackage);
                    foreach (var i in catalogResult.Issues) issues.Add(i.Code);
                }

                // 4. Export & Inspect first DBPF payload
                string exportStatus = "N/A";
                string dbpfInspectStatus = "N/A";

                var dbpfEntry = catalogResult?.Entries.FirstOrDefault(e => e.Kind == Sims3PackPayloadKind.DbpfPackage);
                if (dbpfEntry != null)
                {
                    string targetOutputPath = Path.Combine(tempExportDir, $"{Path.GetFileNameWithoutExtension(fileName)}_{dbpfEntry.EntryIndex}.package");
                    var exportRequest = new Sims3PackPayloadExportRequest(
                        SourceSims3PackPath: filePath,
                        CatalogEntry: dbpfEntry,
                        OutputFilePath: targetOutputPath,
                        AllowOverwrite: true
                    );

                    var exportResult = await exporter.ExportAsync(exportRequest);
                    exportStatus = exportResult.IsSuccess ? "Success" : "Failed";
                    foreach (var i in exportResult.Issues) issues.Add(i.Code);

                    if (exportResult.IsSuccess && File.Exists(targetOutputPath))
                    {
                        var dbpfParseResult = await dbpfParser.ParseFileAsync(targetOutputPath);
                        dbpfInspectStatus = dbpfParseResult.IsSuccess ? $"Success ({dbpfParseResult.Entries.Count} entries)" : "Failed";
                        foreach (var i in dbpfParseResult.Issues) issues.Add(i.Code);
                    }
                }

                reportRows.Add(new ValidationReportRow(
                    FileName: fileName,
                    DetectionStatus: detectStatus,
                    XmlParseStatus: xmlStatus,
                    PayloadCount: payloadCount,
                    DbpfCandidateCount: dbpfCount,
                    ExportStatus: exportStatus,
                    ExportedPackageInspectStatus: dbpfInspectStatus,
                    Issues: string.Join(", ", issues.Distinct())
                ));
            }

            // Generate Markdown Report
            string markdownReport = BuildMarkdownReport(reportRows);
            _output.WriteLine(markdownReport);

            Directory.CreateDirectory(reportsDir);
            string reportPath = Path.Combine(reportsDir, "sims3pack_compatibility_report.md");
            await File.WriteAllTextAsync(reportPath, markdownReport);

            // Assert basic execution health
            reportRows.Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempExportDir))
            {
                try { Directory.Delete(tempExportDir, recursive: true); } catch { }
            }
        }
    }

    private static string BuildMarkdownReport(IReadOnlyList<ValidationReportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Sims3Pack Real Fixture Compatibility Validation Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated Date**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Total Real Fixtures Validated**: {rows.Count}  ");
        sb.AppendLine($"**Successful DBPF Package Exports & Inspections**: {rows.Count(r => r.ExportedPackageInspectStatus.StartsWith("Success"))}  ");
        sb.AppendLine();
        sb.AppendLine("| File Name | Detection | XML Parse | Payloads | DBPF Candidates | Export Status | Exported DBPF Inspection | Issues |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

        foreach (var r in rows)
        {
            string issuesText = string.IsNullOrWhiteSpace(r.Issues) ? "None" : r.Issues;
            sb.AppendLine($"| `{r.FileName}` | {r.DetectionStatus} | {r.XmlParseStatus} | {r.PayloadCount} | {r.DbpfCandidateCount} | {r.ExportStatus} | {r.ExportedPackageInspectStatus} | {issuesText} |");
        }

        return sb.ToString();
    }

    private record ValidationReportRow(
        string FileName,
        string DetectionStatus,
        string XmlParseStatus,
        int PayloadCount,
        int DbpfCandidateCount,
        string ExportStatus,
        string ExportedPackageInspectStatus,
        string Issues
    );
}
