using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Contracts;

public interface ITs4GeomCanonicalMeshImporter
{
    Ts4GeomImportResult Import(byte[] buffer, string? meshName = null);
    Task<Ts4GeomImportResult> ImportAsync(byte[] buffer, string? meshName = null, CancellationToken cancellationToken = default);
}
