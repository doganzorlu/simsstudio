using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Contracts;

public interface IPackageDetector
{
    PackageDetectionResult Detect(ReadOnlySpan<byte> buffer);
    Task<PackageDetectionResult> DetectAsync(Stream? stream, CancellationToken cancellationToken = default);
    Task<PackageDetectionResult> DetectFileAsync(string? filePath, CancellationToken cancellationToken = default);
}
