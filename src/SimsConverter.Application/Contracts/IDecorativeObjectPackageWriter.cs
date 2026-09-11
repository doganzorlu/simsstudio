using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface IDecorativeObjectPackageWriter
{
    DecorativeObjectPackageWriteResult WritePackage(DecorativeObjectPackageWritePlan plan);
    Task<DecorativeObjectPackageWriteResult> WritePackageAsync(DecorativeObjectPackageWritePlan plan, CancellationToken cancellationToken = default);
}
