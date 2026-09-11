using System;
using SimsConverter.Mesh.Models;

namespace SimsConverter.Mesh.Contracts;

public interface ITs4GeomMetadataReader
{
    Ts4GeomParseResult ReadMetadata(ReadOnlySpan<byte> buffer, string? resourceKey = null);
    Ts4GeomParseResult ReadMetadata(byte[] buffer, string? resourceKey = null);
}
