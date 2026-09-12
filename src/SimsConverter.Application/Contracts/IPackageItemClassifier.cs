using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;
using SimsConverter.Package.Models;

namespace SimsConverter.Application.Contracts;

public interface IPackageItemClassifier
{
    PackageItemClassificationResult ClassifyPackage(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources);

    Task<PackageItemClassificationResult> ClassifyPackageAsync(
        string packagePath,
        IReadOnlyList<PackageResourceRow> packageResources,
        CancellationToken cancellationToken = default);
}
