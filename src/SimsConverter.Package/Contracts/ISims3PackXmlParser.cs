using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;

namespace SimsConverter.Package.Contracts;

public interface ISims3PackXmlParser
{
    Task<Sims3PackParseResult> ParseFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<Sims3PackParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
