using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Textures.Contracts;

public interface ITextureResourceExtractor
{
    Task<TextureResourceExtractResult> ExtractAsync(
        TextureResourceExtractRequest request,
        CancellationToken cancellationToken = default);
}
