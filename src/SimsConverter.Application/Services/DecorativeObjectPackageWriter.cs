using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Models;
using SimsConverter.Package.Services;

namespace SimsConverter.Application.Services;

public class DecorativeObjectPackageWriter : IDecorativeObjectPackageWriter
{
    private readonly IDbpfPackageWriter _dbpfPackageWriter;

    public DecorativeObjectPackageWriter(IDbpfPackageWriter? dbpfPackageWriter = null)
    {
        _dbpfPackageWriter = dbpfPackageWriter ?? new DbpfPackageWriter();
    }

    public async Task<DecorativeObjectPackageWriteResult> WritePackageAsync(
        DecorativeObjectPackageWritePlan plan,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => WritePackage(plan), cancellationToken).ConfigureAwait(false);
    }

    public DecorativeObjectPackageWriteResult WritePackage(DecorativeObjectPackageWritePlan plan)
    {
        var issues = new List<ConversionIssue>();

        if (plan == null)
        {
            issues.Add(new ConversionIssue("WRIT000", "Package write plan is null.", ConversionIssueSeverity.Error));
            return new DecorativeObjectPackageWriteResult(false, string.Empty, 0, 0, issues.AsReadOnly());
        }

        if (plan.Issues != null && plan.Issues.Count > 0)
        {
            issues.AddRange(plan.Issues);
        }

        if (!plan.IsPlanValid || plan.PlannedResources == null)
        {
            issues.Add(new ConversionIssue("WRIT000", "Package write plan is invalid.", ConversionIssueSeverity.Error));
            return new DecorativeObjectPackageWriteResult(false, plan.TargetOutputPath ?? string.Empty, 0, 0, issues.AsReadOnly());
        }

        var packageResources = plan.PlannedResources
            .Select(r => new DbpfPackageWriteResourceEntry(
                ResourceId: r.ResourceId,
                Payload: r.Payload,
                CompressionKind: r.CompressionKind,
                DecompressedSize: r.DecompressedSize))
            .ToList();

        var dbpfResult = _dbpfPackageWriter.WritePackage(plan.SourcePackagePath, plan.TargetOutputPath, packageResources);

        if (dbpfResult.Issues != null && dbpfResult.Issues.Count > 0)
        {
            issues.AddRange(dbpfResult.Issues);
        }

        return new DecorativeObjectPackageWriteResult(
            IsSuccess: dbpfResult.IsSuccess,
            TargetOutputPath: dbpfResult.TargetOutputPath,
            ResourceCount: dbpfResult.ResourceCount,
            BytesWritten: dbpfResult.BytesWritten,
            Issues: issues.AsReadOnly()
        );
    }
}
