using System;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Application.Models;

namespace SimsConverter.Application.Contracts;

public interface IBatchConversionService
{
    Task<BatchConversionResult> ScanFolderAsync(
        string sourceFolderPath,
        string? outputFolderPath = null,
        CancellationToken cancellationToken = default);

    Task<BatchConversionResult> ExecuteBatchConversionAsync(
        BatchConversionRequest request,
        IProgress<BatchConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
