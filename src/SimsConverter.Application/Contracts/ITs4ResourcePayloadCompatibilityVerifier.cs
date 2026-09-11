using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Contracts;

public interface ITs4ResourcePayloadCompatibilityVerifier
{
    /// <summary>
    /// Verifies that all TS4 resource payloads in a written package follow valid TS4 binary formats
    /// and that all TGI references point strictly to existing resources in the package index.
    /// </summary>
    Task<Ts4PayloadCompatibilityResult> VerifyPackagePayloadsAsync(
        string packagePath,
        DbpfParseResult parseResult,
        CancellationToken cancellationToken = default);

    Ts4PayloadCompatibilityResult VerifyPackagePayloads(
        string packagePath,
        DbpfParseResult parseResult);
}
