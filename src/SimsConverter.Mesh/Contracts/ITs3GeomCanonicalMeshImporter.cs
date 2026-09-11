using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Contracts;

public interface ITs3GeomCanonicalMeshImporter
{
    Ts3GeomImportResult Import(ReadOnlySpan<byte> buffer, string? nameHint = null);
    Ts3GeomImportResult Import(Stream stream, string? nameHint = null);
    Task<Ts3GeomImportResult> ImportAsync(Stream stream, string? nameHint = null, CancellationToken cancellationToken = default);
}
