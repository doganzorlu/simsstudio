using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Mesh.Contracts;

public interface IMeshResourceExtractor
{
    Task<MeshResourceExtractResult> ExtractAsync(MeshResourceExtractRequest request, CancellationToken cancellationToken = default);
}
