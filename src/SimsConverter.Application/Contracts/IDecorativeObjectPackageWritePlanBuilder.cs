using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface IDecorativeObjectPackageWritePlanBuilder
{
    DecorativeObjectPackageWritePlan BuildWritePlan(DecorativeObjectConversionInputBundle bundle);
    Task<DecorativeObjectPackageWritePlan> BuildWritePlanAsync(DecorativeObjectConversionInputBundle bundle, CancellationToken cancellationToken = default);
}
