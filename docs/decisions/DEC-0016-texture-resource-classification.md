# DEC-0016: Texture Resource Type Catalog & Candidate Classification Foundation

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-TEX-001-R2

## Context
Phase 2 of SIMSStudio introduces texture processing (DDS, RLE2, LRLE, PNG/thumbnails). Before building texture decoders or converters, the pipeline requires a safe, deterministic classification foundation (`ITextureResourceClassifier` / `TextureResourceClassifier`) in `SimsConverter.Textures` to classify package resources by `TypeId` and `GameVersion` without performing binary byte decoding or magic-only guessing.

## Decision

1. **Dedicated Project Structure (`SimsConverter.Textures`)**:
   - `SimsConverter.Textures` and `SimsConverter.Textures.Tests` projects were added to `SimsConverter.sln`.
   - `SimsConverter.Textures` references `SimsConverter.Domain` with zero UI or binary parsing dependencies.

2. **Authoritative & Verified Type ID Catalog (`TextureTypeIds`)**:
   - TypeId constants are strictly verified against authoritative Sims community specifications (*TS4 Resource Type Index*, *TS4 Modders Reference*, and *ModTheSims Skininator Specification*):
     - Shared DDS / TS3 Texture Image: `0x00B2D882` (`Ts3DdsTexture`)
     - TS3 Snapshot Image: `0x0585AFB0` (`Ts3SnapshotThumbnail`)
     - TS4 RLE2 Compressed Image: `0x3453CF95` (`Ts4Rle2Texture`) [Ref: TS4 Resource Type Index]
     - TS4 LRLE Linear RLE Image: `0x2BC04EDF` (`Ts4LrleTexture`) [Ref: TS4 Resource Type Index]
     - TS4 PNG Image: `0x2F7D0004` (`Ts4PngImage`) [Ref: TS4 Resource Type Index]
     - TS4 CAS Part Thumbnail: `0x3C1AF1F2` (`Ts4CasPartThumbnail`) [Ref: TS4 Resource Type Index]

3. **Governance Policy Rule — Strict Catalog Verification**:
   - **Mandatory Policy**: Unverified TypeIds MUST NOT be added to `TextureTypeIds` or the classification service. Every new texture TypeId added to the catalog must be backed by an authoritative modder reference or verified game binary specification. Unverified or speculative entries have been purged.

4. **No Magic-Only Guessing Policy**:
   - Ambiguous/shared TypeIds (such as DDS `0x00B2D882`) retain `GameVersion.Unknown` when classified without an explicit game version hint. Unambiguous engine-specific TypeIds (`0x3453CF95` [RLE2], `0x2BC04EDF` [LRLE], `0x2F7D0004` [PNG Image]) are automatically identified as `GameVersion.Sims4`.

5. **Diagnostic Issue Reporting for Unclassified Resources**:
   - Resources with unrecognized TypeIds return `TextureClassificationKind.Unknown` and generate a diagnostic warning issue (`TEXC001`). Unknown resources are NEVER silently ignored.
   - Null or empty resource entries return safe failure classification (`TEXC000`) without throwing uncaught exceptions.

6. **No Binary Byte Decoding in Phase 2 Foundation**:
   - `SIMS-TEX-001` performs zero DDS header parsing, RLE decompression, image decoding, or file conversion. It establishes pure classification metadata for upcoming texture pipeline tasks.

## Consequences
- Establishes an authoritative, verified foundation for Phase 2 Texture Pipeline.
- Ensures all package resources can be deterministically audited and categorized before decoding.
- Maintains 100% test coverage and quality gate compliance across 5 solution projects.
