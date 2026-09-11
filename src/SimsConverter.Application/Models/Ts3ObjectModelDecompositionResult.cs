using System;
using System.Collections.Generic;
using System.Linq;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Application.Models;

/// <summary>
/// Immutable aggregation result representing decoded TS3 Object Model decomposition resources (MODL, MLOD, RIG, RSLT).
/// </summary>
public record Ts3ObjectModelDecompositionResult(
    bool IsSuccess,
    string SourcePackagePath,
    IReadOnlyList<PackageResourceRow> ModlResources,
    IReadOnlyList<PackageResourceRow> MlodResources,
    IReadOnlyList<PackageResourceRow> RigResources,
    IReadOnlyList<PackageResourceRow> RsltResources,
    IReadOnlyList<Ts3ObjectModelMetadataResult> ModelMetadataResults,
    IReadOnlyList<ConversionIssue> Issues
)
{
    public int ModlCount => ModlResources?.Count ?? 0;
    public int MlodCount => MlodResources?.Count ?? 0;
    public int RigCount => RigResources?.Count ?? 0;
    public int RsltCount => RsltResources?.Count ?? 0;
    public int TotalModelCount => ModlCount + MlodCount;
    public bool HasDecompositionMetadata => (ModlCount > 0 || MlodCount > 0) && ModelMetadataResults != null && ModelMetadataResults.Any(m => m.IsSuccess);

    public static Ts3ObjectModelDecompositionResult Failure(
        string sourcePackagePath,
        string code,
        string message,
        IReadOnlyList<ConversionIssue>? existingIssues = null)
    {
        var issues = new List<ConversionIssue>();
        if (existingIssues != null)
        {
            issues.AddRange(existingIssues);
        }
        issues.Add(new ConversionIssue(code, message, ConversionIssueSeverity.Error));

        return new Ts3ObjectModelDecompositionResult(
            IsSuccess: false,
            SourcePackagePath: sourcePackagePath ?? string.Empty,
            ModlResources: Array.Empty<PackageResourceRow>(),
            MlodResources: Array.Empty<PackageResourceRow>(),
            RigResources: Array.Empty<PackageResourceRow>(),
            RsltResources: Array.Empty<PackageResourceRow>(),
            ModelMetadataResults: Array.Empty<Ts3ObjectModelMetadataResult>(),
            Issues: issues.AsReadOnly()
        );
    }
}
