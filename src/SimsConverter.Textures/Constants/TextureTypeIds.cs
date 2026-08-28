namespace SimsConverter.Textures.Constants;

public static class TextureTypeIds
{
    // Shared / TS3 DDS Texture Format
    public const uint Ts3DdsTexture = 0x00B2D882;

    // TS3 Auxiliary Image Formats
    public const uint Ts3SnapshotThumbnail = 0x0585AFB0;

    // TS4 Specific Texture Formats (Verified against TS4 Resource Type Index & Modders Reference)
    public const uint Ts4Rle2Texture = 0x3453CF95;
    public const uint Ts4LrleTexture = 0x2BC04EDF;
    public const uint Ts4PngImage = 0x2F7D0004;
    public const uint Ts4CasPartThumbnail = 0x3C1AF1F2;
}
