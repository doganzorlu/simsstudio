using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Contracts;

namespace SimsConverter.Textures.Services;

public class Ts4Rle2TextureDecoder : ITs4Rle2TextureDecoder
{
    private const uint Rle2Magic = 0x32454C52u; // "RLE2" in Little Endian
    private const ushort ExpectedVersion = 2;
    private const int HeaderSize = 14;

    public Ts4Rle2DecodeResult Decode(ReadOnlySpan<byte> rle2Payload, string resourceKey)
    {
        if (rle2Payload.Length < HeaderSize)
        {
            return Ts4Rle2DecodeResult.Failure("RLE2001", $"RLE2 payload size ({rle2Payload.Length} bytes) is less than required header size (14 bytes) for resource '{resourceKey}'.");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(rle2Payload.Slice(0, 4));
        if (magic != Rle2Magic)
        {
            string magicStr = Encoding.ASCII.GetString(rle2Payload.Slice(0, 4));
            return Ts4Rle2DecodeResult.Failure("RLE2001", $"Invalid RLE2 magic signature '{magicStr}' (0x{magic:X8}) for resource '{resourceKey}'. Expected 'RLE2'.");
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(rle2Payload.Slice(4, 2));
        if (version != ExpectedVersion)
        {
            return Ts4Rle2DecodeResult.Failure("RLE2001", $"Invalid RLE2 version {version} for resource '{resourceKey}'. Expected version {ExpectedVersion}.");
        }

        ushort width = BinaryPrimitives.ReadUInt16LittleEndian(rle2Payload.Slice(6, 2));
        ushort height = BinaryPrimitives.ReadUInt16LittleEndian(rle2Payload.Slice(8, 2));
        ushort mipCount = BinaryPrimitives.ReadUInt16LittleEndian(rle2Payload.Slice(10, 2));
        ushort format = BinaryPrimitives.ReadUInt16LittleEndian(rle2Payload.Slice(12, 2));

        if (width == 0 || height == 0 || mipCount == 0)
        {
            return Ts4Rle2DecodeResult.Failure("RLE2002", $"Invalid RLE2 dimensions ({width}x{height}, mips={mipCount}) for resource '{resourceKey}'.");
        }

        int tableSize = 16 * mipCount;
        if (rle2Payload.Length < HeaderSize + tableSize)
        {
            return Ts4Rle2DecodeResult.Failure("RLE2003", $"RLE2 mipmap offset table truncated ({rle2Payload.Length} bytes available, {HeaderSize + tableSize} required) for resource '{resourceKey}'.");
        }

        DdsTextureFormatKind formatKind;
        string fourCc;
        int blockSize;

        switch (format)
        {
            case 0x0000:
                formatKind = DdsTextureFormatKind.Dxt1;
                fourCc = "DXT1";
                blockSize = 8;
                break;
            case 0x0001:
                formatKind = DdsTextureFormatKind.Dxt5;
                fourCc = "DXT5";
                blockSize = 16;
                break;
            case 0x0002:
                formatKind = DdsTextureFormatKind.Ati1;
                fourCc = "ATI1";
                blockSize = 8;
                break;
            case 0x0003:
                formatKind = DdsTextureFormatKind.Ati2;
                fourCc = "ATI2";
                blockSize = 16;
                break;
            default:
                return Ts4Rle2DecodeResult.Failure("RLE2002", $"Unsupported RLE2 format index 0x{format:X4} for resource '{resourceKey}'.");
        }

        var decodedMipLevels = new List<DecodedRle2MipLevel>();
        using var allPixelsMs = new MemoryStream();

        int tableOffset = HeaderSize;

        for (uint i = 0; i < mipCount; i++)
        {
            uint cmdOffset = BinaryPrimitives.ReadUInt32LittleEndian(rle2Payload.Slice(tableOffset, 4));
            uint pixOffset = BinaryPrimitives.ReadUInt32LittleEndian(rle2Payload.Slice(tableOffset + 4, 4));
            uint cmdCount = BinaryPrimitives.ReadUInt32LittleEndian(rle2Payload.Slice(tableOffset + 8, 4));
            uint pixSize = BinaryPrimitives.ReadUInt32LittleEndian(rle2Payload.Slice(tableOffset + 12, 4));

            tableOffset += 16;

            if (cmdOffset + cmdCount > rle2Payload.Length)
            {
                return Ts4Rle2DecodeResult.Failure("RLE2004", $"RLE2 command stream out of bounds (Offset={cmdOffset}, Count={cmdCount}, Payload={rle2Payload.Length}) for mip level {i} in resource '{resourceKey}'.");
            }

            if (pixOffset + pixSize > rle2Payload.Length)
            {
                return Ts4Rle2DecodeResult.Failure("RLE2004", $"RLE2 pixel stream out of bounds (Offset={pixOffset}, Count={pixSize}, Payload={rle2Payload.Length}) for mip level {i} in resource '{resourceKey}'.");
            }

            int shift = (int)Math.Min(i, 31);
            uint mipW = Math.Max(1, (uint)width >> shift);
            uint mipH = Math.Max(1, (uint)height >> shift);

            uint blocksX = Math.Max(1, (mipW + 3) / 4);
            uint blocksY = Math.Max(1, (mipH + 3) / 4);
            uint expectedBlocks = blocksX * blocksY;

            ReadOnlySpan<byte> cmdSpan = rle2Payload.Slice((int)cmdOffset, (int)cmdCount);
            ReadOnlySpan<byte> pixSpan = rle2Payload.Slice((int)pixOffset, (int)pixSize);

            int cmdIdx = 0;
            int pixIdx = 0;
            uint decodedBlocks = 0;
            using var mipPixelsMs = new MemoryStream();

            while (cmdIdx < cmdSpan.Length && decodedBlocks < expectedBlocks)
            {
                byte cmd = cmdSpan[cmdIdx++];

                if ((cmd & 0x80) != 0)
                {
                    // Uncompressed run: count = (cmd & 0x7F) + 1 blocks
                    uint runBlocks = (uint)((cmd & 0x7F) + 1);
                    int runBytes = (int)(runBlocks * (uint)blockSize);

                    if (pixIdx + runBytes > pixSpan.Length)
                    {
                        return Ts4Rle2DecodeResult.Failure("RLE2005", $"RLE2 pixel stream exhausted reading uncompressed run ({runBytes} bytes required, {pixSpan.Length - pixIdx} available) for mip level {i} in resource '{resourceKey}'.");
                    }

                    mipPixelsMs.Write(pixSpan.Slice(pixIdx, runBytes));
                    pixIdx += runBytes;
                    decodedBlocks += runBlocks;
                }
                else
                {
                    // RLE / Repeated run: count = (cmd & 0x7F) + 1 blocks
                    uint runBlocks = (uint)((cmd & 0x7F) + 1);

                    if (pixSpan.Length == 0)
                    {
                        // Zero / transparent run
                        byte[] zeroBlock = new byte[blockSize];
                        for (uint b = 0; b < runBlocks; b++)
                        {
                            mipPixelsMs.Write(zeroBlock);
                        }
                    }
                    else
                    {
                        // Single reference block repeated
                        if (pixIdx + blockSize > pixSpan.Length)
                        {
                            return Ts4Rle2DecodeResult.Failure("RLE2005", $"RLE2 pixel stream exhausted reading reference block for compressed run for mip level {i} in resource '{resourceKey}'.");
                        }

                        ReadOnlySpan<byte> refBlock = pixSpan.Slice(pixIdx, blockSize);
                        pixIdx += blockSize;

                        for (uint b = 0; b < runBlocks; b++)
                        {
                            mipPixelsMs.Write(refBlock);
                        }
                    }

                    decodedBlocks += runBlocks;
                }
            }

            if (decodedBlocks != expectedBlocks)
            {
                return Ts4Rle2DecodeResult.Failure("RLE2006", $"RLE2 command stream decoded {decodedBlocks} 4x4 blocks, expected {expectedBlocks} blocks for mip level {i} in resource '{resourceKey}'.");
            }

            byte[] mipPixelBytes = mipPixelsMs.ToArray();
            allPixelsMs.Write(mipPixelBytes);

            decodedMipLevels.Add(new DecodedRle2MipLevel(
                MipIndex: i,
                Width: mipW,
                Height: mipH,
                BlockCount: expectedBlocks,
                PixelData: mipPixelBytes
            ));
        }

        // Build valid 128-byte DDS header for decoded DDS payload output
        byte[] ddsHeader = CreateDdsHeader(width, height, mipCount, fourCc);
        byte[] ddsPixelBytes = allPixelsMs.ToArray();
        byte[] fullDdsPayload = new byte[ddsHeader.Length + ddsPixelBytes.Length];
        ddsHeader.CopyTo(fullDdsPayload, 0);
        ddsPixelBytes.CopyTo(fullDdsPayload, ddsHeader.Length);

        return new Ts4Rle2DecodeResult(
            IsSuccess: true,
            DecodedDdsPayload: fullDdsPayload,
            Width: width,
            Height: height,
            MipMapCount: mipCount,
            FormatKind: formatKind,
            MipLevels: decodedMipLevels.AsReadOnly(),
            Issues: Array.Empty<ConversionIssue>()
        );
    }

    private static byte[] CreateDdsHeader(uint width, uint height, uint mipCount, string fourCc)
    {
        byte[] buffer = new byte[128];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x00, 4), 0x20534444); // "DDS "
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x04, 4), 124); // dwSize
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x08, 4), 0x00021007); // dwFlags (CAPS | HEIGHT | WIDTH | PIXELFORMAT | MIPMAPCOUNT)
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x0C, 4), height);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x10, 4), width);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x1C, 4), mipCount);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x4C, 4), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x50, 4), 0x04);
        byte[] fourCcBytes = Encoding.ASCII.GetBytes(fourCc.PadRight(4, '\0'));
        Array.Copy(fourCcBytes, 0, buffer, 0x54, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0x6C, 4), 0x1000);
        return buffer;
    }
}
