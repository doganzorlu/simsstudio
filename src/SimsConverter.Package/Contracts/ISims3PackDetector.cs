using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Contracts;

public interface ISims3PackDetector
{
    Task<PackageDetectionResult> DetectFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<PackageDetectionResult> DetectAsync(
        Stream stream,
        string? fileName = null,
        CancellationToken cancellationToken = default);
}
