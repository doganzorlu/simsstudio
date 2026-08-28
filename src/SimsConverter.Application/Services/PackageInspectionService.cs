using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Application.Services;

public class PackageInspectionService : IPackageInspectionService
{
    private readonly IDbpfPackageParser _parser;

    public PackageInspectionService(IDbpfPackageParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public async Task<PackageInspectionResult> InspectFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return PackageInspectionResult.Failure(
                filePath ?? string.Empty,
                "INSPECT001",
                "Specified file path is null, empty, or whitespace."
            );
        }

        try
        {
            DbpfParseResult parseResult = await _parser.ParseFileAsync(filePath, cancellationToken);

            var rows = new List<PackageResourceRow>(parseResult.Entries.Count);
            foreach (var entry in parseResult.Entries)
            {
                rows.Add(PackageResourceRow.FromEntry(entry));
            }

            return new PackageInspectionResult(
                parseResult.IsSuccess,
                filePath,
                parseResult.Header,
                rows.AsReadOnly(),
                parseResult.Issues
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PackageInspectionResult.Failure(
                filePath,
                "INSPECT002",
                $"Package inspection encountered an unexpected error: {ex.Message}"
            );
        }
    }
}
