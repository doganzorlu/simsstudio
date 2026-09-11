using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface IDecorativeObjectConversionService
{
    Task<DecorativeObjectConversionResult> CreateConversionPlanAsync(
        DecorativeObjectConversionRequest request,
        CancellationToken cancellationToken = default);

    DecorativeObjectConversionResult CreateConversionPlan(
        DecorativeObjectConversionRequest request);

    Task<DecorativeObjectConversionResult> ExecuteConversionAsync(
        DecorativeObjectConversionRequest request,
        CancellationToken cancellationToken = default);

    DecorativeObjectConversionResult ExecuteConversion(
        DecorativeObjectConversionRequest request);
}
