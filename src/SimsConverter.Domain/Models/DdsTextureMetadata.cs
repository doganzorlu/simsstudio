using SimsConverter.Domain.Enums;

namespace SimsConverter.Domain.Models;

public record DdsTextureMetadata(
    uint Width,
    uint Height,
    uint MipMapCount,
    DdsTextureFormatKind FormatKind,
    string FourCC,
    uint PixelFormatFlags,
    uint RgbBitCount,
    uint Caps
);
