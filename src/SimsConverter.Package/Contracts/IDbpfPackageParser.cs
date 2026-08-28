using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Contracts;

public interface IDbpfPackageParser
{
    DbpfParseResult Parse(ReadOnlySpan<byte> buffer);
    Task<DbpfParseResult> ParseAsync(Stream? stream, CancellationToken cancellationToken = default);
    Task<DbpfParseResult> ParseFileAsync(string? filePath, CancellationToken cancellationToken = default);
}
