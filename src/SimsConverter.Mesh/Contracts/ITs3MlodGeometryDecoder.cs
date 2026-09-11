using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Contracts;

public interface ITs3MlodGeometryDecoder
{
    Ts3GeomImportResult Decode(ReadOnlySpan<byte> buffer, string? nameHint = null);
}
