using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Contracts;

public interface ITs4PackageConsumerValidator
{
    Ts4ConsumerValidationResult ValidatePackage(string packageFilePath);

    Task<Ts4ConsumerValidationResult> ValidatePackageAsync(
        string packageFilePath,
        CancellationToken cancellationToken = default);
}
