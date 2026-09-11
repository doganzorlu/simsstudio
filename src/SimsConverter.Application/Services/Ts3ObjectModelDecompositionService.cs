using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Constants;
using SimsConverter.Mesh.Contracts;
using SimsConverter.Mesh.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Application.Services;

/// <summary>
/// Implementation of TS3 Object Model Decomposition Service.
/// Collects MODL (0x01661233), MLOD (0x01D10F34), RIG (0x8EAF13DE), and RSLT (0xD3044521) resources,
/// reads decompressed MODL/MLOD payloads strictly via IPackageResourcePayloadReader, and aggregates ITs3ObjectModelMetadataReader results.
/// </summary>
public class Ts3ObjectModelDecompositionService : ITs3ObjectModelDecompositionService
{
    private readonly IPackageInspectionService _packageInspectionService;
    private readonly IPackageResourcePayloadReader _payloadReader;
    private readonly ITs3ObjectModelMetadataReader _metadataReader;

    public Ts3ObjectModelDecompositionService(
        IPackageInspectionService packageInspectionService,
        IPackageResourcePayloadReader payloadReader,
        ITs3ObjectModelMetadataReader metadataReader)
    {
        _packageInspectionService = packageInspectionService ?? throw new ArgumentNullException(nameof(packageInspectionService));
        _payloadReader = payloadReader ?? throw new ArgumentNullException(nameof(payloadReader));
        _metadataReader = metadataReader ?? throw new ArgumentNullException(nameof(metadataReader));
    }

    public async Task<Ts3ObjectModelDecompositionResult> DecomposeAsync(
        string packageFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageFilePath))
        {
            return Ts3ObjectModelDecompositionResult.Failure(
                packageFilePath ?? string.Empty,
                "DECOMP000",
                "Package file path is null or empty."
            );
        }

        var packageResult = await _packageInspectionService.InspectFileAsync(packageFilePath, cancellationToken).ConfigureAwait(false);
        if (!packageResult.IsSuccess)
        {
            return Ts3ObjectModelDecompositionResult.Failure(
                packageFilePath,
                "DECOMP001",
                $"Package inspection failed for file: {packageFilePath}",
                packageResult.Issues
            );
        }

        return await DecomposeAsyncInternal(packageResult, cancellationToken).ConfigureAwait(false);
    }

    public Ts3ObjectModelDecompositionResult Decompose(PackageInspectionResult packageInspection)
    {
        if (packageInspection == null)
        {
            return Ts3ObjectModelDecompositionResult.Failure(
                string.Empty,
                "DECOMP000",
                "Package inspection result is null."
            );
        }

        if (!packageInspection.IsSuccess)
        {
            return Ts3ObjectModelDecompositionResult.Failure(
                packageInspection.FilePath ?? string.Empty,
                "DECOMP001",
                "Package inspection result indicates failure.",
                packageInspection.Issues
            );
        }

        var modlRows = new List<PackageResourceRow>();
        var mlodRows = new List<PackageResourceRow>();
        var rigRows = new List<PackageResourceRow>();
        var rsltRows = new List<PackageResourceRow>();
        var metaResults = new List<Ts3ObjectModelMetadataResult>();
        var issues = new List<ConversionIssue>();

        if (packageInspection.Issues != null)
        {
            issues.AddRange(packageInspection.Issues);
        }

        if (packageInspection.Resources != null)
        {
            foreach (var row in packageInspection.Resources)
            {
                if (row.TypeId == MeshTypeIds.TsSharedModel) // MODL 0x01661233
                {
                    modlRows.Add(row);
                    ProcessModelRow(packageInspection.FilePath, row, metaResults, issues);
                }
                else if (row.TypeId == MeshTypeIds.TsSharedModelLod) // MLOD 0x01D10F34
                {
                    mlodRows.Add(row);
                    ProcessModelRow(packageInspection.FilePath, row, metaResults, issues);
                }
                else if (row.TypeId == MeshTypeIds.TsSharedRig) // RIG 0x8EAF13DE
                {
                    rigRows.Add(row);
                }
                else if (row.TypeId == MeshTypeIds.TsSharedSlot) // RSLT 0xD3044521
                {
                    rsltRows.Add(row);
                }
            }
        }

        bool isSuccess = packageInspection.IsSuccess &&
                         !issues.Any(i => i.Severity == ConversionIssueSeverity.Error) &&
                         metaResults.All(m => m.IsSuccess);

        return new Ts3ObjectModelDecompositionResult(
            IsSuccess: isSuccess,
            SourcePackagePath: packageInspection.FilePath ?? string.Empty,
            ModlResources: modlRows.AsReadOnly(),
            MlodResources: mlodRows.AsReadOnly(),
            RigResources: rigRows.AsReadOnly(),
            RsltResources: rsltRows.AsReadOnly(),
            ModelMetadataResults: metaResults.AsReadOnly(),
            Issues: issues.AsReadOnly()
        );
    }

    private async Task<Ts3ObjectModelDecompositionResult> DecomposeAsyncInternal(
        PackageInspectionResult packageInspection,
        CancellationToken cancellationToken)
    {
        var modlRows = new List<PackageResourceRow>();
        var mlodRows = new List<PackageResourceRow>();
        var rigRows = new List<PackageResourceRow>();
        var rsltRows = new List<PackageResourceRow>();
        var metaResults = new List<Ts3ObjectModelMetadataResult>();
        var issues = new List<ConversionIssue>();

        if (packageInspection.Issues != null)
        {
            issues.AddRange(packageInspection.Issues);
        }

        if (packageInspection.Resources != null)
        {
            foreach (var row in packageInspection.Resources)
            {
                if (row.TypeId == MeshTypeIds.TsSharedModel) // MODL 0x01661233
                {
                    modlRows.Add(row);
                    await ProcessModelRowAsync(packageInspection.FilePath, row, metaResults, issues, cancellationToken).ConfigureAwait(false);
                }
                else if (row.TypeId == MeshTypeIds.TsSharedModelLod) // MLOD 0x01D10F34
                {
                    mlodRows.Add(row);
                    await ProcessModelRowAsync(packageInspection.FilePath, row, metaResults, issues, cancellationToken).ConfigureAwait(false);
                }
                else if (row.TypeId == MeshTypeIds.TsSharedRig) // RIG 0x8EAF13DE
                {
                    rigRows.Add(row);
                }
                else if (row.TypeId == MeshTypeIds.TsSharedSlot) // RSLT 0xD3044521
                {
                    rsltRows.Add(row);
                }
            }
        }

        bool isSuccess = packageInspection.IsSuccess &&
                         !issues.Any(i => i.Severity == ConversionIssueSeverity.Error) &&
                         metaResults.All(m => m.IsSuccess);

        return new Ts3ObjectModelDecompositionResult(
            IsSuccess: isSuccess,
            SourcePackagePath: packageInspection.FilePath ?? string.Empty,
            ModlResources: modlRows.AsReadOnly(),
            MlodResources: mlodRows.AsReadOnly(),
            RigResources: rigRows.AsReadOnly(),
            RsltResources: rsltRows.AsReadOnly(),
            ModelMetadataResults: metaResults.AsReadOnly(),
            Issues: issues.AsReadOnly()
        );
    }

    private void ProcessModelRow(
        string packagePath,
        PackageResourceRow row,
        List<Ts3ObjectModelMetadataResult> metaResults,
        List<ConversionIssue> issues)
    {
        var entry = CreateEntryFromRow(row);
        var payloadResult = _payloadReader.ReadPayload(packagePath, entry);

        if (payloadResult.Issues != null && payloadResult.Issues.Count > 0)
        {
            issues.AddRange(payloadResult.Issues);
        }

        if (payloadResult.IsSuccess && payloadResult.Payload != null)
        {
            var metaResult = _metadataReader.Read(payloadResult.Payload, entry.Id);
            metaResults.Add(metaResult);
            if (metaResult.Issues != null && metaResult.Issues.Count > 0)
            {
                issues.AddRange(metaResult.Issues);
            }
        }
        else
        {
            var kind = row.TypeId == MeshTypeIds.TsSharedModel ? Ts3ObjectModelKind.Modl : Ts3ObjectModelKind.Mlod;
            string errorMsg = $"Payload extraction failed for model entry {entry.Id.FormattedKey}.";
            var failureResult = Ts3ObjectModelMetadataResult.Failure(
                entry.Id,
                kind,
                "DECOMP002",
                errorMsg
            );
            metaResults.Add(failureResult);
            issues.Add(new ConversionIssue("DECOMP002", errorMsg, ConversionIssueSeverity.Error));
        }
    }

    private async Task ProcessModelRowAsync(
        string packagePath,
        PackageResourceRow row,
        List<Ts3ObjectModelMetadataResult> metaResults,
        List<ConversionIssue> issues,
        CancellationToken cancellationToken)
    {
        var entry = CreateEntryFromRow(row);
        var payloadResult = await _payloadReader.ReadPayloadAsync(packagePath, entry, cancellationToken).ConfigureAwait(false);

        if (payloadResult.Issues != null && payloadResult.Issues.Count > 0)
        {
            issues.AddRange(payloadResult.Issues);
        }

        if (payloadResult.IsSuccess && payloadResult.Payload != null)
        {
            var metaResult = _metadataReader.Read(payloadResult.Payload, entry.Id);
            metaResults.Add(metaResult);
            if (metaResult.Issues != null && metaResult.Issues.Count > 0)
            {
                issues.AddRange(metaResult.Issues);
            }
        }
        else
        {
            var kind = row.TypeId == MeshTypeIds.TsSharedModel ? Ts3ObjectModelKind.Modl : Ts3ObjectModelKind.Mlod;
            string errorMsg = $"Payload extraction failed for model entry {entry.Id.FormattedKey}.";
            var failureResult = Ts3ObjectModelMetadataResult.Failure(
                entry.Id,
                kind,
                "DECOMP002",
                errorMsg
            );
            metaResults.Add(failureResult);
            issues.Add(new ConversionIssue("DECOMP002", errorMsg, ConversionIssueSeverity.Error));
        }
    }

    private static PackageResourceEntry CreateEntryFromRow(PackageResourceRow row)
    {
        return new PackageResourceEntry(
            new PackageResourceId(row.TypeId, row.GroupId, row.InstanceId),
            row.Offset,
            row.CompressedSize,
            row.DecompressedSize,
            row.CompressionKind,
            0
        );
    }
}
