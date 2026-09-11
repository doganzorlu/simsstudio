using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Contracts;

namespace SimsConverter.Textures.Services;

public class Ts4Rle2TexturePayloadBuilder : ITs4Rle2TexturePayloadBuilder
{
    private const uint Rle2Magic = 0x32454C52u; // "RLE2" in Little Endian (0x52, 0x4C, 0x45, 0x32)
    private const ushort Rle2Version = 2;
    private const int DdsHeaderSize = 128;

    private readonly IDdsHeaderParser _headerParser;
    private readonly IDdsPayloadValidator _payloadValidator;

    public Ts4Rle2TexturePayloadBuilder(
        IDdsHeaderParser? headerParser = null,
        IDdsPayloadValidator? payloadValidator = null)
    {
        _headerParser = headerParser ?? new DdsHeaderParser();
        _payloadValidator = payloadValidator ?? new DdsPayloadValidator();
    }

    public Ts4Rle2PayloadResult BuildPayload(ReadOnlySpan<byte> ddsPayload, string resourceKey)
    {
        if (ddsPayload.Length < DdsHeaderSize)
        {
            return Ts4Rle2PayloadResult.Failure("TEXR001", $"DDS payload size ({ddsPayload.Length} bytes) is less than header size (128 bytes) for resource '{resourceKey}'.");
        }

        var parseResult = _headerParser.Parse(ddsPayload);
        if (!parseResult.IsSuccess || parseResult.Metadata == null)
        {
            return Ts4Rle2PayloadResult.Failure("TEXR001", $"DDS header parsing failed for resource '{resourceKey}'.", parseResult.Issues);
        }

        var metadata = parseResult.Metadata;

        var valResult = _payloadValidator.Validate(metadata, ddsPayload);
        if (!valResult.IsSuccess)
        {
            return Ts4Rle2PayloadResult.Failure("TEXR002", $"DDS payload validation failed for resource '{resourceKey}'.", valResult.Issues);
        }

        ushort rle2Format;
        int blockSize;

        switch (metadata.FormatKind)
        {
            case DdsTextureFormatKind.Dxt1:
                rle2Format = 0x0000;
                blockSize = 8;
                break;
            case DdsTextureFormatKind.Dxt3:
            case DdsTextureFormatKind.Dxt5:
                rle2Format = 0x0001;
                blockSize = 16;
                break;
            case DdsTextureFormatKind.Ati1:
                rle2Format = 0x0002;
                blockSize = 8;
                break;
            case DdsTextureFormatKind.Ati2:
                rle2Format = 0x0003;
                blockSize = 16;
                break;
            default:
                return Ts4Rle2PayloadResult.Failure("TEXR003", $"Unsupported DDS pixel format '{metadata.FormatKind}' / FourCC '{metadata.FourCC}' for resource '{resourceKey}'.");
        }

        uint width = metadata.Width;
        uint height = metadata.Height;
        uint mipCount = metadata.MipMapCount > 0 ? metadata.MipMapCount : 1;

        var commandStreams = new List<byte[]>();
        var pixelStreams = new List<byte[]>();

        int currentDdsOffset = DdsHeaderSize;

        for (uint i = 0; i < mipCount; i++)
        {
            int shift = (int)Math.Min(i, 31);
            uint mipW = Math.Max(1, width >> shift);
            uint mipH = Math.Max(1, height >> shift);

            uint blocksX = Math.Max(1, (mipW + 3) / 4);
            uint blocksY = Math.Max(1, (mipH + 3) / 4);
            uint totalBlocks = blocksX * blocksY;

            int levelBytes = (int)(totalBlocks * (uint)blockSize);
            if (currentDdsOffset + levelBytes > ddsPayload.Length)
            {
                return Ts4Rle2PayloadResult.Failure("TEXR004", $"DDS payload truncated reading mipmap level {i} ({levelBytes} bytes required) for resource '{resourceKey}'.");
            }

            ReadOnlySpan<byte> levelDdsData = ddsPayload.Slice(currentDdsOffset, levelBytes);
            currentDdsOffset += levelBytes;

            // Generate RLE2 command stream for raw uncompressed runs of DXT blocks
            using var cmdMs = new MemoryStream();
            uint remainingBlocks = totalBlocks;

            while (remainingBlocks > 0)
            {
                uint runCount = Math.Min(remainingBlocks, 128);
                // Uncompressed run command byte: 0x80 + (runCount - 1)
                byte cmd = (byte)(0x80 + (runCount - 1));
                cmdMs.WriteByte(cmd);
                remainingBlocks -= runCount;
            }

            commandStreams.Add(cmdMs.ToArray());
            pixelStreams.Add(levelDdsData.ToArray());
        }

        // Header (14 bytes) + Index Table (16 bytes * mipCount) + Commands + Pixels
        int headerSize = 14;
        int tableSize = 16 * (int)mipCount;
        int totalCommandSize = 0;
        int totalPixelSize = 0;

        for (int i = 0; i < (int)mipCount; i++)
        {
            totalCommandSize += commandStreams[i].Length;
            totalPixelSize += pixelStreams[i].Length;
        }

        int totalPayloadSize = headerSize + tableSize + totalCommandSize + totalPixelSize;
        byte[] payload = new byte[totalPayloadSize];
        var payloadSpan = payload.AsSpan();

        // 1. Write Header (14 bytes)
        BinaryPrimitives.WriteUInt32LittleEndian(payloadSpan.Slice(0, 4), Rle2Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(payloadSpan.Slice(4, 2), Rle2Version);
        BinaryPrimitives.WriteUInt16LittleEndian(payloadSpan.Slice(6, 2), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(payloadSpan.Slice(8, 2), (ushort)height);
        BinaryPrimitives.WriteUInt16LittleEndian(payloadSpan.Slice(10, 2), (ushort)mipCount);
        BinaryPrimitives.WriteUInt16LittleEndian(payloadSpan.Slice(12, 2), rle2Format);

        // 2. Compute offsets for Index Table
        uint cmdOffset = (uint)(headerSize + tableSize);
        uint pixOffset = (uint)(headerSize + tableSize + totalCommandSize);

        int tableOffset = headerSize;
        for (int i = 0; i < (int)mipCount; i++)
        {
            uint cmdCount = (uint)commandStreams[i].Length;
            uint pixSize = (uint)pixelStreams[i].Length;

            BinaryPrimitives.WriteUInt32LittleEndian(payloadSpan.Slice(tableOffset, 4), cmdOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(payloadSpan.Slice(tableOffset + 4, 4), pixOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(payloadSpan.Slice(tableOffset + 8, 4), cmdCount);
            BinaryPrimitives.WriteUInt32LittleEndian(payloadSpan.Slice(tableOffset + 12, 4), pixSize);

            tableOffset += 16;
            cmdOffset += cmdCount;
            pixOffset += pixSize;
        }

        // 3. Write Command Streams
        int cmdWritePos = headerSize + tableSize;
        for (int i = 0; i < (int)mipCount; i++)
        {
            commandStreams[i].CopyTo(payload, cmdWritePos);
            cmdWritePos += commandStreams[i].Length;
        }

        // 4. Write Pixel Data Streams
        int pixWritePos = headerSize + tableSize + totalCommandSize;
        for (int i = 0; i < (int)mipCount; i++)
        {
            pixelStreams[i].CopyTo(payload, pixWritePos);
            pixWritePos += pixelStreams[i].Length;
        }

        return new Ts4Rle2PayloadResult(
            IsSuccess: true,
            Payload: payload,
            Width: width,
            Height: height,
            MipMapCount: mipCount,
            Rle2Format: rle2Format,
            Issues: Array.Empty<ConversionIssue>()
        );
    }
}
