# DEC-0017: Texture Resource Raw Extraction Boundary Architecture

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-TEX-002-R1

## Context
Phase 2 of SIMSStudio requires a safe, texture-specific extraction boundary (`ITextureResourceExtractor` / `TextureResourceExtractor`) in `SimsConverter.Textures` to extract raw binary texture payloads from DBPF packages without duplicating low-level byte-copying logic or performing image decoding/conversion in this step.

## Decision

1. **Delegation to Package Layer (`IPackageResourceExporter`)**:
   - `TextureResourceExtractor` delegates binary byte range copying directly to `IPackageResourceExporter` in `SimsConverter.Package`. It does NOT duplicate byte stream read/write loops or atomic file creation patterns.

2. **Classification Enforcement Guard**:
   - Extraction is allowed strictly when `request.Classification.Classification == TextureClassificationKind.KnownTexture`. Unclassified or unknown resources are rejected with controlled error `TEXE001`.

3. **Classification-Entry Identity Guard (`TEXE004`)**:
   - Extraction verifies that `request.Classification.ResourceId == request.Entry.Id`. If a caller passes a `KnownTexture` classification that belongs to a different resource entry identity, extraction is immediately aborted with controlled error `TEXE004` ("Classification ResourceId does not match resource entry identity.") before invoking `IPackageResourceExporter`.

4. **Deterministic Extension Mapping**:
   - Mapped extensions by TypeId:
     - `0x00B2D882` (Shared DDS Texture) -> `.dds`
     - `0x3453CF95` (TS4 RLE2 Compressed Image) -> `.rle2`
     - `0x2BC04EDF` (TS4 LRLE Linear RLE Image) -> `.lrle`
     - `0x2F7D0004` (TS4 PNG Image), `0x0585AFB0` (TS3 Snapshot Image), `0x3C1AF1F2` (TS4 CAS Part Thumbnail) -> `.png`
     - Unmapped / Unknown TypeIds -> `.raw`

5. **Canonical Path Separation & Overwrite Policy**:
   - Canonical path separation check (`TEXE008`) prevents target output path from matching the source package path.
   - `AllowOverwrite == false` by default; existing output files are preserved unless explicitly overridden, and the `AllowOverwrite` flag is propagated directly to `PackageResourceExportRequest`.

6. **No Image Decoding / Conversion in SIMS-TEX-002**:
   - Does NOT decode DDS headers, RLE2/LRLE compressed blocks, PNG pixels, or convert image formats. Establishes the safe raw extraction boundary for upcoming texture decoders.

## Consequences
- Establishes a clean, safe texture payload extraction boundary with resource identity validation.
- Leverages existing Package layer atomic file policies (`EXPE008`, `EXPE005`).
- Maintains 100% test coverage and quality gate compliance across 5 solution projects.
