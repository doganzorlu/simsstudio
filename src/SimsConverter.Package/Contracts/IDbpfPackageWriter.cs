using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Package.Models;

namespace SimsConverter.Package.Contracts;

public interface IDbpfPackageWriter
{
    DbpfPackageWriteResult WritePackage(
        string sourcePackagePath,
        string targetOutputPath,
        IEnumerable<DbpfPackageWriteResourceEntry> resources);

    Task<DbpfPackageWriteResult> WritePackageAsync(
        string sourcePackagePath,
        string targetOutputPath,
        IEnumerable<DbpfPackageWriteResourceEntry> resources,
        CancellationToken cancellationToken = default);
}
