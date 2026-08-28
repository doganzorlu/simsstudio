using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Textures.Contracts;

public interface IDdsHeaderParser
{
    DdsParseResult Parse(ReadOnlySpan<byte> data);
    DdsParseResult Parse(Stream stream);
    Task<DdsParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}
