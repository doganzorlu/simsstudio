# DEC-0011: Sims3Pack Embedded Payload Catalog Scanner Architecture

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-S3P-003-R2

## Context
SIMSStudio requires a read-only, non-destructive streaming catalog scanner to inspect the entire archive section of `.sims3pack` containers after validating outer binary headers and skipping XML metadata manifests, without extracting payloads, creating temporary files, or allocating large arrays.

## Decision

1. **Allocation-Free XML Skip & 10MB Guard (`S3PC007`)**:
   - `Sims3PackPayloadCatalogScanner` enforces `xmlLength <= MaxXmlMetadataBytes` (10MB limit). If `xmlLength` exceeds 10MB, `ScanAsync` returns controlled failure `S3PC007` without allocating memory.
   - For seekable streams, skipping moves the stream pointer directly via `stream.Seek(archiveOffset, SeekOrigin.Begin)`. For non-seekable streams, XML bytes are skipped using a small 4KB dummy buffer loop. Zero large array allocation!

2. **Chunked Streaming Scan & Boundary Overlap Window (50MB Limit)**:
   - Scans the archive using a chunked streaming loop (8KB chunks) up to `MaxArchiveScanBytes = 50MB`.
   - Maintains a 7-byte overlap window between consecutive chunks so magic signatures (`"DBPF"` 4 bytes, PNG magic 8 bytes) spanning chunk boundaries are NEVER missed.
   - If total scanned archive bytes reach 50MB, scanning stops safely and adds controlled warning issue `S3PC006` ("Archive scan reached maximum limit of 50MB").

3. **Overlap Window Candidate De-duplication**:
   - Tracks scanned offset keys `(Kind, DataOffset)` in a hash set to prevent signatures in chunk overlap regions from producing duplicate entries.
   - Guarantees that each candidate payload produces at most ONE catalog entry at its exact `DataOffset`.

4. **Bounded Catalog Entry Limit & Warning (`S3PC008`)**:
   - Scanned catalog entries are sorted deterministically by `DataOffset`.
   - Capped at `MaxCatalogEntries = 100`. If entry count reaches 100, scanning stops cleanly and adds controlled warning issue `S3PC008` ("Catalog entry count reached maximum limit of 100 entries.").
   - Empty archive sections (no payload bytes after XML section) return a successful result (`IsSuccess == true`) with an empty catalog list.

## Consequences
- Guarantees zero duplicate entries when scanning across chunk overlap boundaries.
- Informs callers when catalog entries are truncated via controlled warning issue `S3PC008`.
- Protects source files and memory state against corruption or uncaught exceptions.
- Establishes a solid foundation for future embedded package extraction tasks (`SIMS-S3P-004`).
