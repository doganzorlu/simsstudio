using System;
using SimsConverter.Domain.Enums;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Models;

namespace SimsConverter.Package.Services;

public class StubPackageInspector : IPackageInspector
{
    private static readonly byte[] DbpfMagic = "DBPF"u8.ToArray();

    public PackageHeaderSummary InspectHeader(ReadOnlySpan<byte> headerBuffer)
    {
        if (headerBuffer.Length < 4)
        {
            return new PackageHeaderSummary("0", "0", 0, GameVersion.Unknown);
        }

        bool isDbpf = headerBuffer[..4].SequenceEqual(DbpfMagic);
        if (!isDbpf)
        {
            return new PackageHeaderSummary("0", "0", 0, GameVersion.Unknown);
        }

        // Magic DBPF header confirmed, but game version cannot be guessed from magic alone (TS3 and TS4 both use DBPF containers).
        // Requires detailed resource/header inspection to determine exact game version.
        return new PackageHeaderSummary("2", "0", 1, GameVersion.Unknown);
    }
}
