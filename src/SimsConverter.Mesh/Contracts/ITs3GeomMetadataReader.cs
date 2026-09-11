using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Contracts;

public interface ITs3GeomMetadataReader
{
    Ts3GeomParseResult Read(ReadOnlySpan<byte> buffer);
    Ts3GeomParseResult Read(Stream stream);
    Task<Ts3GeomParseResult> ReadAsync(Stream stream, CancellationToken cancellationToken = default);
}
