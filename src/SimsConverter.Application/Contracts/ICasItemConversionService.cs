using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface ICasItemConversionService
{
    Task<DecorativeObjectConversionResult> ConvertCasPackageAsync(
        DecorativeObjectConversionRequest request,
        CancellationToken cancellationToken = default);
}
