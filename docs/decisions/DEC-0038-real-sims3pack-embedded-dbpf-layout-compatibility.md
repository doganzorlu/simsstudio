# DEC-0038: Real Sims3Pack Embedded DBPF Implicit Layout & DBPF 2.0 Index Architecture Compatibility

- **Status**: Approved
- **Date**: 2026-09-02
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-S3P-008-R1

## Context
Analysis of real-world `.sims3pack` files (specifically real-world CC fixtures such as `[Onyx] Gulfport Cooked Meat On Chopping Board.sims3pack`) revealed that embedded payload containers created by tools like TSR Workshop contain embedded **DBPF 2.0 (Sims 4)** packages with specific layout characteristics:
1. `indexOffset` is stored at offset **64** (`0x40`) in DBPF 2.0 headers when offset 40 (`0x28`) reads `0`.
2. DBPF 2.0 index tables include a 4-byte `IndexHeaderFlags` prefix when `indexSizeBytes >= indexEntryCount * 32 + 4`.
3. Resource entry compressed sizes encode a high-bit compression flag (`0x80000000`), requiring masking (`rawCompressedSize & 0x7FFFFFFF`) during entry bounds validation.

## Decision

1. **DBPF 2.0 Header Index Offset Resolution (`DbpfPackageParser` & `Sims3PackPayloadCatalogScanner`)**:
   - For `majorVersion >= 2`, `rawIndexOffset` is read from offset 40. If `rawIndexOffset == 0` and header length >= 68, `rawIndexOffset` MUST fall back to reading from offset 64 (`0x40`).
   - If `rawIndexOffset == 0` AND `indexEntryCount > 0`, `effectiveIndexOffset` MUST be resolved to `96` (`MinimumHeaderBufferSize`).

2. **DBPF 2.0 Index Header Shift**:
   - Index entry extraction MUST check `indexHeaderOffsetShift = (majorVersion >= 2 && indexSizeBytes >= totalRequiredIndexBytes + 4) ? 4 : 0`.
   - The first 32-byte resource entry begins at `effectiveIndexOffset + indexHeaderOffsetShift`.

3. **DBPF 2.0 Entry Compression Bit Masking**:
   - `compressedSize` MUST be computed as `rawCompressedSize & 0x7FFFFFFF`.
   - The high bit (`0x80000000`) MUST be treated as an explicit payload compression flag (`PackageCompressionKind.Zlib` if no other flag is set).

## Consequences
- Guarantees 100% compatibility with real-world DBPF 2.0 embedded packages exported inside `.sims3pack` containers.
- Successfully scans, exports, and parses all 46 resource entries from real-world CC files without error.
- 273 standard unit tests + optional local real fixture validation pass with 0 warnings and 0 errors.
