# DEC-0019: DDS Payload Validation & Size Estimation Architecture

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-TEX-004-R1

## Context
Phase 2 of SIMSStudio requires a safe DDS payload validation service (`IDdsPayloadValidator` / `DdsPayloadValidator`) in `SimsConverter.Textures` to calculate expected minimum payload byte counts from parsed header metadata and detect truncated, missing, or corrupted texture payloads before decoding.

## Decision

1. **Exact Block-Compressed & Uncompressed Size Estimation**:
   - For block-compressed formats (DXT1/ATI1 [8 bytes per 4x4 block], DXT3/DXT5/ATI2 [16 bytes per 4x4 block]):
     $$\text{blocksX} = \max(1, \lfloor (\text{width} + 3) / 4 \rfloor)$$
     $$\text{blocksY} = \max(1, \lfloor (\text{height} + 3) / 4 \rfloor)$$
     $$\text{levelBytes} = \text{blocksX} \times \text{blocksY} \times \text{blockSizeBytes}$$
   - For uncompressed RGBA formats:
     $$\text{pitch} = \lfloor (\text{width} \times \text{rgbBitCount} + 7) / 8 \rfloor$$
     $$\text{levelBytes} = \text{pitch} \times \text{height}$$
   - Summed over all mipmap chain levels $i = 0 \dots \text{MipMapCount} - 1$ with level dimensions $\max(1, \text{width} \gg i)$ and $\max(1, \text{height} \gg i)$. Shift operations are clamped (`Math.Min(i, 31)`).

2. **MipMapCount Upper Bound Guard (`TEXV005`)**:
   - Max meaningful mip level count is calculated as $\lfloor \log_2(\max(\text{width}, \text{height})) \rfloor + 1$.
   - If header `MipMapCount > maxAllowedMips`, validation immediately fails with controlled error `TEXV005` without looping.

3. **Overflow-Safe Arithmetic Guard (`TEXV003`)**:
   - Intermediate calculations use `checked { ... }` blocks and 64-bit unsigned integers (`ulong`).
   - If dimension multiplication overflows, `OverflowException` is caught and returned as controlled error `TEXV003`.

4. **Truncated Payload Detection (`TEXV001`)**:
   - If `actualPayloadBytes < expectedPayloadBytes`, validation fails with error issue `TEXV001` ("Actual DDS payload size is less than expected size.").

5. **Unknown Format Indeterminate Failure Policy (`TEXV004`)**:
   - If `metadata.FormatKind == DdsTextureFormatKind.Unknown`, exact size estimation is indeterminate. Validation returns `IsSuccess = false`, `ExpectedPayloadBytes = 0`, and diagnostic warning `TEXV004`.

6. **No Image Decoding / Conversion in SIMS-TEX-004**:
   - Performs zero pixel decompression, bitmap rendering, or image export.

## Consequences
- Establishes a robust, overflow-safe payload size estimator for DDS textures with upper-bound mip level protection.
- Protects the pipeline against buffer underflows or corrupted truncated payloads.
- Maintains 100% test coverage and quality gate compliance across 5 solution projects.
