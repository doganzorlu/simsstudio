using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Models;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Contracts;

/// <summary>
/// Read-only metadata reader boundary for TS3 MODL (0x01661233) and MLOD (0x01D10F34) object model resources.
/// Expects decompressed payload bytes.
/// </summary>
public interface ITs3ObjectModelMetadataReader
{
    /// <summary>
    /// Reads MODL/MLOD metadata from a decompressed binary payload span.
    /// </summary>
    Ts3ObjectModelMetadataResult Read(ReadOnlySpan<byte> buffer, PackageResourceId resourceId);

    /// <summary>
    /// Reads MODL/MLOD metadata from a decompressed binary payload stream, preserving stream position if seekable.
    /// </summary>
    Ts3ObjectModelMetadataResult Read(Stream stream, PackageResourceId resourceId);

    /// <summary>
    /// Asynchronously reads MODL/MLOD metadata from a decompressed binary payload stream, preserving stream position if seekable.
    /// </summary>
    Task<Ts3ObjectModelMetadataResult> ReadAsync(Stream stream, PackageResourceId resourceId, CancellationToken cancellationToken = default);
}
