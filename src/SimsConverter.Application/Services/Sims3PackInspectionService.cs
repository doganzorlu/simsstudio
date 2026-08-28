using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Application.Services;

public class Sims3PackInspectionService : ISims3PackInspectionService
{
    private readonly ISims3PackDetector _detector;
    private readonly ISims3PackXmlParser _xmlParser;
    private readonly ISims3PackPayloadCatalogScanner _catalogScanner;
    private readonly ISims3PackPayloadExporter _exporter;

    private static readonly Regex InvalidFileNameCharRegex = new(@"[\x00-\x1F\x7F\x22\x3C\x3E\x7C\x3A\x2A\x3F\x5C\x2F]", RegexOptions.Compiled);

    public Sims3PackInspectionService(
        ISims3PackDetector detector,
        ISims3PackXmlParser xmlParser,
        ISims3PackPayloadCatalogScanner catalogScanner,
        ISims3PackPayloadExporter exporter)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _xmlParser = xmlParser ?? throw new ArgumentNullException(nameof(xmlParser));
        _catalogScanner = catalogScanner ?? throw new ArgumentNullException(nameof(catalogScanner));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    public async Task<Sims3PackInspectionResult> InspectFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Sims3PackInspectionResult.Failure(filePath ?? string.Empty, "S3PA000", "File path is invalid or file does not exist.");
        }

        var aggregatedIssues = new List<ConversionIssue>();

        // Step 1: Detect container
        var detectionResult = await _detector.DetectFileAsync(filePath, cancellationToken);
        if (detectionResult.Issues.Count > 0)
        {
            aggregatedIssues.AddRange(detectionResult.Issues);
        }

        if (detectionResult.ContainerKind != PackageContainerKind.Sims3Pack)
        {
            return new Sims3PackInspectionResult(
                IsSuccess: false,
                FilePath: filePath,
                RootElementName: null,
                DeclaredEncoding: null,
                RawXmlSizeBytes: null,
                Title: null,
                AssetId: null,
                AssetType: null,
                Description: null,
                PayloadRows: Array.Empty<Sims3PackPayloadRow>(),
                Issues: aggregatedIssues.AsReadOnly()
            );
        }

        // Step 2: Parse XML metadata
        var xmlResult = await _xmlParser.ParseFileAsync(filePath, cancellationToken);
        if (xmlResult.Issues.Count > 0)
        {
            aggregatedIssues.AddRange(xmlResult.Issues);
        }

        if (!xmlResult.IsSuccess || xmlResult.Metadata == null)
        {
            // Partial success: XML parse failed -> DO NOT run catalog scanner!
            return new Sims3PackInspectionResult(
                IsSuccess: false,
                FilePath: filePath,
                RootElementName: null,
                DeclaredEncoding: null,
                RawXmlSizeBytes: null,
                Title: null,
                AssetId: null,
                AssetType: null,
                Description: null,
                PayloadRows: Array.Empty<Sims3PackPayloadRow>(),
                Issues: aggregatedIssues.AsReadOnly()
            );
        }

        // Step 3: Scan archive catalog
        var catalogResult = await _catalogScanner.ScanFileAsync(filePath, cancellationToken);
        if (catalogResult.Issues.Count > 0)
        {
            aggregatedIssues.AddRange(catalogResult.Issues);
        }

        var payloadRows = new List<Sims3PackPayloadRow>();
        if (catalogResult.IsSuccess && catalogResult.Entries.Count > 0)
        {
            foreach (var entry in catalogResult.Entries)
            {
                bool canExport = entry.Kind == Sims3PackPayloadKind.DbpfPackage;
                payloadRows.Add(new Sims3PackPayloadRow(
                    EntryIndex: entry.EntryIndex,
                    Kind: entry.Kind.ToString(),
                    DataOffset: entry.DataOffset,
                    DataOffsetHex: $"0x{entry.DataOffset:X8}",
                    EstimatedSizeBytes: entry.EstimatedSizeBytes,
                    EstimatedSizeFormatted: FormatSize(entry.EstimatedSizeBytes),
                    DisplayName: entry.DisplayName ?? $"Payload #{entry.EntryIndex}",
                    CanExport: canExport,
                    Issues: entry.Issues
                ));
            }
        }

        return new Sims3PackInspectionResult(
            IsSuccess: catalogResult.IsSuccess,
            FilePath: filePath,
            RootElementName: xmlResult.Metadata.RootElementName,
            DeclaredEncoding: xmlResult.Metadata.DeclaredEncoding,
            RawXmlSizeBytes: xmlResult.Metadata.RawXmlSizeBytes,
            Title: xmlResult.Metadata.Title,
            AssetId: xmlResult.Metadata.AssetId,
            AssetType: xmlResult.Metadata.AssetType,
            Description: xmlResult.Metadata.Description,
            PayloadRows: payloadRows.AsReadOnly(),
            Issues: aggregatedIssues.AsReadOnly()
        );
    }

    public async Task<Sims3PackExportResult> ExportPayloadAsync(
        Sims3PackExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Sims3PackExportResult.Failure(string.Empty, string.Empty, "S3PA000", "Export request is null.");
        }

        if (request.SelectedRow == null || !request.SelectedRow.CanExport || request.SelectedRow.Kind != Sims3PackPayloadKind.DbpfPackage.ToString())
        {
            return Sims3PackExportResult.Failure(request.SourceSims3PackPath ?? string.Empty, string.Empty, "S3PA005", "Selected payload row cannot be exported as DBPF package.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return Sims3PackExportResult.Failure(request.SourceSims3PackPath ?? string.Empty, string.Empty, "S3PA003", "Output directory is invalid or empty.");
        }

        // Generate sanitized target output filename
        string rawFileName = !string.IsNullOrWhiteSpace(request.CustomFileName)
            ? request.CustomFileName
            : (!string.IsNullOrWhiteSpace(request.SelectedRow.DisplayName) ? request.SelectedRow.DisplayName : $"package_{request.SelectedRow.EntryIndex:D3}");

        string sanitizedFileName = InvalidFileNameCharRegex.Replace(rawFileName, "_").Trim();
        if (string.IsNullOrWhiteSpace(sanitizedFileName))
        {
            sanitizedFileName = $"package_{request.SelectedRow.EntryIndex:D3}";
        }

        if (!sanitizedFileName.EndsWith(".package", StringComparison.OrdinalIgnoreCase))
        {
            sanitizedFileName += ".package";
        }

        string targetOutputPath = Path.Combine(request.OutputDirectory, sanitizedFileName);

        // Reconstruct domain catalog entry from presentation row
        var catalogEntry = new Sims3PackCatalogEntry(
            EntryIndex: request.SelectedRow.EntryIndex,
            Kind: Sims3PackPayloadKind.DbpfPackage,
            DataOffset: request.SelectedRow.DataOffset,
            EstimatedSizeBytes: request.SelectedRow.EstimatedSizeBytes,
            DisplayName: request.SelectedRow.DisplayName,
            Issues: request.SelectedRow.Issues
        );

        var domainRequest = new Sims3PackPayloadExportRequest(
            SourceSims3PackPath: request.SourceSims3PackPath,
            CatalogEntry: catalogEntry,
            OutputFilePath: targetOutputPath,
            AllowOverwrite: request.AllowOverwrite
        );

        var domainResult = await _exporter.ExportAsync(domainRequest, cancellationToken);

        return new Sims3PackExportResult(
            IsSuccess: domainResult.IsSuccess,
            SourceSims3PackPath: domainResult.SourceSims3PackPath,
            OutputFilePath: domainResult.OutputFilePath,
            ExportedBytes: domainResult.ExportedBytes,
            Issues: domainResult.Issues
        );
    }

    private static string FormatSize(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value < 0)
        {
            return "Unknown";
        }

        double val = bytes.Value;
        if (val < 1024)
        {
            return $"{val:F0} B";
        }
        val /= 1024.0;
        if (val < 1024)
        {
            return $"{val:F1} KB";
        }
        val /= 1024.0;
        return $"{val:F2} MB";
    }
}
