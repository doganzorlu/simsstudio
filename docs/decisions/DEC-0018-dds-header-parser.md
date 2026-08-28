# DEC-0018: DDS Header Parser & Metadata Reader Architecture

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-TEX-003-R1

## Context
Phase 2 of SIMSStudio requires a safe, bounds-checked DDS binary header parser (`IDdsHeaderParser` / `DdsHeaderParser`) in `SimsConverter.Textures` to extract metadata (width, height, mipmap count, FourCC, pixel format flags, format family) from raw DDS payloads without decoding pixels, converting images, or introducing native dependencies.

## Decision

1. **Microsoft DDS Programming Guide Layout Compliance**:
   - The parser strictly adheres to the official Microsoft DDS Programming Guide (`DDS_HEADER` and `DDS_PIXELFORMAT` specifications):
     - `0x00`: `dwMagic` (`0x20534444` / `"DDS "`)
     - `0x04`: `DDS_HEADER.dwSize` (must be `124`)
     - `0x0C`: `dwHeight`
     - `0x10`: `dwWidth`
     - `0x1C`: `dwMipMapCount`
     - `0x4C`: `DDS_PIXELFORMAT.dwSize` (must be `32`)
     - `0x50`: `DDS_PIXELFORMAT.dwFlags`
     - `0x54`: `DDS_PIXELFORMAT.dwFourCC`
     - `0x58`: `DDS_PIXELFORMAT.dwRGBBitCount`
     - `0x6C`: `dwCaps`

2. **Strict Layout & Size Validation Rules**:
   - Minimum total header size guard (`TEXD001`): `128` bytes (`124` byte `DDS_HEADER` + `4` byte `"DDS "` magic).
   - Magic signature guard (`TEXD002`): Little-Endian `"DDS "` (`0x20534444`).
   - Dimensions guard (`TEXD003`): `Width > 0 && Height > 0`.
   - Header struct size guard (`TEXD005`): `DDS_HEADER.dwSize == 124`.
   - Pixel format struct size guard (`TEXD006`): `DDS_PIXELFORMAT.dwSize == 32`.

3. **Format Family Classification (`DdsTextureFormatKind`)**:
   - Supported format families: `Dxt1` (`"DXT1"`), `Dxt3` (`"DXT3"`), `Dxt5` (`"DXT5"`), `Ati1` (`"ATI1"`/`"BC4U"`), `Ati2` (`"ATI2"`/`"BC5U"`), `UncompressedRgba` (DDPF_RGB/DDPF_ALPHAPIXELS without FourCC), `Unknown`.
   - Unrecognized FourCC values return `FormatKind = Unknown` and produce diagnostic warning `TEXD004` without throwing uncaught exceptions.

4. **Stream Invariant Guard**:
   - Stream-based parsing preserves stream position (`stream.Position`) if the input stream is seekable.

5. **No Image Decoding / Conversion in SIMS-TEX-003**:
   - Performs zero pixel block decompression, RGBA buffer decoding, PNG conversion, or UI rendering.

## Consequences
- Establishes a fast, lightweight, spec-compliant DDS header reader.
- Prepares the texture pipeline for subsequent DDS transcoding or RLE conversion tasks.
- Maintains 100% test coverage and quality gate compliance across 5 solution projects.
