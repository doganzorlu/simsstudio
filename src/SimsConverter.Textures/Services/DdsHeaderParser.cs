using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;
using SimsConverter.Textures.Contracts;

namespace SimsConverter.Textures.Services;

public class DdsHeaderParser : IDdsHeaderParser
{
    private const int HeaderSizeBytes = 128;
    private const uint DdsMagic = 0x20534444; // "DDS " in Little Endian
    private const uint ExpectedHeaderDwSize = 124;
    private const uint ExpectedPixelFormatDwSize = 32;

    public DdsParseResult Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSizeBytes)
        {
            return DdsParseResult.Failure(
                "TEXD001",
                $"DDS payload size ({data.Length} bytes) is less than required header size ({HeaderSizeBytes} bytes)."
            );
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
        if (magic != DdsMagic)
        {
            return DdsParseResult.Failure(
                "TEXD002",
                $"Invalid DDS magic signature 0x{magic:X8}. Expected 'DDS ' (0x20534444)."
            );
        }

        uint headerDwSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x04, 4));
        if (headerDwSize != ExpectedHeaderDwSize)
        {
            return DdsParseResult.Failure(
                "TEXD005",
                $"Invalid DDS header dwSize ({headerDwSize}). Expected {ExpectedHeaderDwSize}."
            );
        }

        uint height = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x0C, 4));
        uint width = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x10, 4));
        uint mipMapCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x1C, 4));
        if (mipMapCount == 0)
        {
            mipMapCount = 1; // Standard DDS default when mipmaps not specified
        }

        if (width == 0 || height == 0)
        {
            return DdsParseResult.Failure(
                "TEXD003",
                $"Invalid DDS texture dimensions (Width: {width}, Height: {height}). Dimensions must be positive."
            );
        }

        uint pfSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x4C, 4));
        if (pfSize != ExpectedPixelFormatDwSize)
        {
            return DdsParseResult.Failure(
                "TEXD006",
                $"Invalid DDS pixel format dwSize ({pfSize}). Expected {ExpectedPixelFormatDwSize}."
            );
        }

        uint pfFlags = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x50, 4));
        ReadOnlySpan<byte> fourCcBytes = data.Slice(0x54, 4);
        string fourCc = GetFourCcString(fourCcBytes);
        uint rgbBitCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x58, 4));
        uint caps = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x6C, 4));

        var (formatKind, issue) = DetermineFormatKind(fourCc, pfFlags);

        var issuesList = new List<ConversionIssue>();
        if (issue != null)
        {
            issuesList.Add(issue);
        }

        var metadata = new DdsTextureMetadata(
            Width: width,
            Height: height,
            MipMapCount: mipMapCount,
            FormatKind: formatKind,
            FourCC: fourCc,
            PixelFormatFlags: pfFlags,
            RgbBitCount: rgbBitCount,
            Caps: caps
        );

        return new DdsParseResult(
            IsSuccess: true,
            Metadata: metadata,
            Issues: issuesList.AsReadOnly()
        );
    }

    public DdsParseResult Parse(Stream stream)
    {
        if (stream == null)
        {
            return DdsParseResult.Failure("TEXD000", "Stream is null.");
        }

        long originalPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            byte[] headerBuffer = new byte[HeaderSizeBytes];
            int bytesRead = ReadFullStream(stream, headerBuffer);
            if (bytesRead < HeaderSizeBytes)
            {
                return DdsParseResult.Failure(
                    "TEXD001",
                    $"DDS stream length ({bytesRead} bytes) is less than required header size ({HeaderSizeBytes} bytes)."
                );
            }

            return Parse(headerBuffer);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    public async Task<DdsParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            return DdsParseResult.Failure("TEXD000", "Stream is null.");
        }

        long originalPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            byte[] headerBuffer = new byte[HeaderSizeBytes];
            int bytesRead = await ReadFullStreamAsync(stream, headerBuffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead < HeaderSizeBytes)
            {
                return DdsParseResult.Failure(
                    "TEXD001",
                    $"DDS stream length ({bytesRead} bytes) is less than required header size ({HeaderSizeBytes} bytes)."
                );
            }

            return Parse(headerBuffer);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    private static string GetFourCcString(ReadOnlySpan<byte> bytes)
    {
        if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0)
        {
            return string.Empty;
        }

        char[] chars = new char[4];
        for (int i = 0; i < 4; i++)
        {
            char c = (char)bytes[i];
            chars[i] = char.IsControl(c) ? '?' : c;
        }

        return new string(chars).Trim();
    }

    private static (DdsTextureFormatKind FormatKind, ConversionIssue? Issue) DetermineFormatKind(string fourCc, uint pfFlags)
    {
        string upperFourCc = fourCc.ToUpperInvariant();

        switch (upperFourCc)
        {
            case "DXT1":
                return (DdsTextureFormatKind.Dxt1, null);
            case "DXT3":
                return (DdsTextureFormatKind.Dxt3, null);
            case "DXT5":
                return (DdsTextureFormatKind.Dxt5, null);
            case "ATI1":
            case "BC4U":
            case "BC4S":
                return (DdsTextureFormatKind.Ati1, null);
            case "ATI2":
            case "BC5U":
            case "BC5S":
                return (DdsTextureFormatKind.Ati2, null);
        }

        const uint DdpfRgb = 0x40;
        const uint DdpfAlphaPixels = 0x01;

        if (string.IsNullOrEmpty(fourCc) && ((pfFlags & DdpfRgb) != 0 || (pfFlags & DdpfAlphaPixels) != 0))
        {
            return (DdsTextureFormatKind.UncompressedRgba, null);
        }

        var warningIssue = new ConversionIssue(
            "TEXD004",
            $"Unrecognized or unsupported DDS pixel format FourCC '{fourCc}'.",
            ConversionIssueSeverity.Warning
        );

        return (DdsTextureFormatKind.Unknown, warningIssue);
    }

    private static int ReadFullStream(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer.Slice(totalRead));
            if (read == 0) break;
            totalRead += read;
        }

        return totalRead;
    }

    private static async Task<int> ReadFullStreamAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.Slice(totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            totalRead += read;
        }

        return totalRead;
    }
}
