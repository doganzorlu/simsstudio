using System;

namespace SimsConverter.Package.Services;

public static class RefpackDecompressor
{
    public static bool IsRefpackHeader(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty || buffer.Length < 2) return false;
        byte b0 = buffer[0];
        byte b1 = buffer[1];
        return (b1 == 0xFB && (b0 & 0x1F) == 0x10) || (b0 == 0xFB && (b1 & 0x1F) == 0x10);
    }

    public static bool TryDecompress(byte[] compressedData, uint expectedDecompressedSize, out byte[] decompressedData)
    {
        decompressedData = Array.Empty<byte>();
        if (compressedData == null || compressedData.Length < 5)
        {
            return false;
        }

        try
        {
            int inPos = 0;
            byte flags0 = compressedData[inPos++];
            byte flags1 = compressedData[inPos++];

            byte flagByte;
            if (flags1 == 0xFB)
            {
                if ((flags0 & 0x1F) != 0x10) return false;
                flagByte = flags0;
            }
            else if (flags0 == 0xFB)
            {
                if ((flags1 & 0x1F) != 0x10) return false;
                flagByte = flags1;
            }
            else
            {
                return false;
            }

            // Extract decompressed size from header
            uint headerDecompressedSize;
            if ((flagByte & 0x80) != 0)
            {
                if (compressedData.Length < 6) return false;
                headerDecompressedSize = ((uint)compressedData[inPos++] << 24) |
                                         ((uint)compressedData[inPos++] << 16) |
                                         ((uint)compressedData[inPos++] << 8) |
                                         (uint)compressedData[inPos++];
            }
            else
            {
                if (compressedData.Length < 5) return false;
                headerDecompressedSize = ((uint)compressedData[inPos++] << 16) |
                                         ((uint)compressedData[inPos++] << 8) |
                                         (uint)compressedData[inPos++];
            }

            if (headerDecompressedSize == 0)
            {
                return false;
            }

            // Strict Contract Enforcement: Header decompressed size MUST match expectedDecompressedSize when provided (> 0)
            if (expectedDecompressedSize > 0 && headerDecompressedSize != expectedDecompressedSize)
            {
                return false;
            }

            var output = new byte[headerDecompressedSize];
            int outPos = 0;
            bool endReached = false;

            while (inPos < compressedData.Length && outPos < headerDecompressedSize && !endReached)
            {
                byte b0 = compressedData[inPos++];

                int literalCount = 0;
                int copyCount = 0;
                int copyOffset = 0;

                if ((b0 & 0x80) == 0)
                {
                    // 2-byte opcode: 0byte1...
                    if (inPos >= compressedData.Length) break;
                    byte b1 = compressedData[inPos++];

                    literalCount = b0 & 0x03;
                    copyCount = ((b0 & 0x1C) >> 2) + 3;
                    copyOffset = ((b0 & 0x60) << 3) + b1 + 1;
                }
                else if ((b0 & 0x40) == 0)
                {
                    // 3-byte opcode: 10byte1...
                    if (inPos + 1 >= compressedData.Length) break;
                    byte b1 = compressedData[inPos++];
                    byte b2 = compressedData[inPos++];

                    literalCount = b1 >> 6;
                    copyCount = (b0 & 0x3F) + 4;
                    copyOffset = ((b1 & 0x3F) << 8) + b2 + 1;
                }
                else if ((b0 & 0x20) == 0)
                {
                    // 4-byte opcode: 110byte1...
                    if (inPos + 2 >= compressedData.Length) break;
                    byte b1 = compressedData[inPos++];
                    byte b2 = compressedData[inPos++];
                    byte b3 = compressedData[inPos++];

                    literalCount = b0 & 0x03;
                    copyCount = ((b0 & 0x0C) << 6) + b3 + 5;
                    copyOffset = ((b0 & 0x10) << 12) + (b1 << 8) + b2 + 1;
                }
                else
                {
                    // 1-byte opcode: 111byte1...
                    if (b0 >= 0xFC)
                    {
                        literalCount = b0 & 0x03;
                        copyCount = 0;
                        endReached = true;
                    }
                    else
                    {
                        literalCount = (b0 - 0xDF) * 4;
                        copyCount = 0;
                    }
                }

                // Copy literals
                if (literalCount > 0)
                {
                    if (inPos + literalCount > compressedData.Length || outPos + literalCount > headerDecompressedSize)
                    {
                        return false;
                    }
                    Array.Copy(compressedData, inPos, output, outPos, literalCount);
                    inPos += literalCount;
                    outPos += literalCount;
                }

                // Copy matched bytes from output history
                if (copyCount > 0)
                {
                    if (outPos < copyOffset || outPos + copyCount > headerDecompressedSize)
                    {
                        return false;
                    }
                    int matchSrc = outPos - copyOffset;
                    for (int i = 0; i < copyCount; i++)
                    {
                        output[outPos++] = output[matchSrc++];
                    }
                }
            }

            // Strict Contract Check: outPos MUST exactly equal headerDecompressedSize (and expectedDecompressedSize when provided)
            if (outPos == (int)headerDecompressedSize && (expectedDecompressedSize == 0 || outPos == (int)expectedDecompressedSize))
            {
                decompressedData = output;
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
