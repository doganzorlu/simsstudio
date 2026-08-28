using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;
using SimsConverter.Package.Contracts;

namespace SimsConverter.Application.Services;

public class ResourceExportService : IResourceExportService
{
    private readonly IPackageResourceExporter _exporter;

    public ResourceExportService(IPackageResourceExporter exporter)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    public async Task<PackageResourceExportResult> ExportResourceAsync(
        SingleResourceExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return PackageResourceExportResult.Failure(string.Empty, string.Empty, "EXPE000", "Export request cannot be null.");
        }

        if (request.Resource == null)
        {
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath ?? string.Empty,
                string.Empty,
                "EXPE010",
                "Selected resource to export cannot be null."
            );
        }

        if (string.IsNullOrWhiteSpace(request.DestinationDirectory))
        {
            return PackageResourceExportResult.Failure(
                request.SourcePackagePath ?? string.Empty,
                string.Empty,
                "EXPE002",
                "Export destination directory is null, empty, or whitespace."
            );
        }

        string safeFilename = SanitizeFilename(request.Resource.FormattedKey);
        string outputFilePath = Path.Combine(request.DestinationDirectory, safeFilename);

        var packageRequest = new PackageResourceExportRequest(
            request.SourcePackagePath,
            request.Resource.Offset,
            request.Resource.CompressedSize,
            outputFilePath,
            request.AllowOverwrite
        );

        return await _exporter.ExportAsync(packageRequest, cancellationToken);
    }

    public static string SanitizeFilename(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return "unnamed_resource.raw";
        }

        // Replace colon, slashes, backslashes, and path navigation tokens
        string sanitized = rawInput.Replace(':', '_')
                                  .Replace('/', '_')
                                  .Replace('\\', '_')
                                  .Replace("..", "_");

        // Strip cross-platform invalid filename characters (* ? " < > |)
        char[] crossPlatformInvalidChars = new[] { '*', '?', '"', '<', '>', '|' };
        foreach (char invalidChar in crossPlatformInvalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        // Strip OS-specific invalid filename characters
        char[] osInvalidChars = Path.GetInvalidFileNameChars();
        foreach (char invalidChar in osInvalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        // Trim leading and trailing spaces or dots
        sanitized = sanitized.Trim('.', ' ');

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "unnamed_resource.raw";
        }

        if (!sanitized.EndsWith(".raw", StringComparison.OrdinalIgnoreCase))
        {
            sanitized += ".raw";
        }

        return sanitized;
    }
}
