using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace SimsConverter.Package.Services;

public static class PackagePayloadDecompressor
{
    public static bool TryDecompressZlib(byte[] compressedData, uint expectedDecompressedSize, out byte[] decompressedData)
    {
        decompressedData = Array.Empty<byte>();
        if (compressedData == null || compressedData.Length == 0 || compressedData.Length < 2) return false;

        // Reject RefPack payloads in Zlib reader
        if (RefpackDecompressor.IsRefpackHeader(compressedData))
        {
            return false;
        }

        try
        {
            // ZLib header check (0x78)
            int skipHeader = (compressedData[0] == 0x78) ? 2 : 0;
            using var ms = new MemoryStream(compressedData, skipHeader, compressedData.Length - skipHeader);
            using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            byte[] decomp = output.ToArray();

            // Strict deterministic length match: decomp.Length MUST equal expectedDecompressedSize
            if (decomp.Length == (int)expectedDecompressedSize)
            {
                decompressedData = decomp;
                return true;
            }
        }
        catch
        {
            // Decompression error
        }

        return false;
    }
}
