# DEC-0012: Sims3Pack Embedded DBPF Payload Raw Exporter Architecture

- **Status**: Pending Approval
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-S3P-004-R1

## Context
SIMSStudio requires a safe, bounds-checked raw binary exporter to extract embedded DBPF package candidates cataloged from `.sims3pack` containers to user-specified target `.package` files, without interpreting or modifying payload byte contents or corrupting source files.

## Decision

1. **DBPF Kind Enforcement (`S3PE001`)**:
   - `Sims3PackPayloadExporter` ONLY executes export requests where `CatalogEntry.Kind == Sims3PackPayloadKind.DbpfPackage`. Requests for PNG preview images or unknown candidates are rejected with controlled error `S3PE001`.

2. **Non-Destructive Read-Only Source Access & Canonical Path Guard (`S3PE008`)**:
   - Source `.sims3pack` files are accessed read-only (`FileAccess.Read`, `FileShare.Read`).
   - `Sims3PackPayloadExporter` compares `Path.GetFullPath(request.SourceSims3PackPath)` against `Path.GetFullPath(request.OutputFilePath)` using OS-specific case sensitivity rules. If paths are identical, export is aborted with controlled error `S3PE008`.

3. **Guaranteed Temp Cleanup & Atomic File Policy (`S3PE005`, `S3PE006`, `S3PE007`)**:
   - If target file exists and `AllowOverwrite == false`, export is aborted with `S3PE005` leaving existing target untouched.
   - Raw bytes are written to a temporary file (`${outputPath}.tmp.${guid}`) first, verified, and atomically moved to destination via `File.Move(tempFile, outputPath, overwrite: AllowOverwrite)`.
   - **Guaranteed Cleanup**: On ALL failure paths (premature EOF, invalid bounds, magic check failure, cancellation, or exceptions), temporary files are guaranteed to be deleted before method return via `finally` blocks and explicit cleanup routines (`CleanupTempFile`). Zero leftover temporary files!

4. **Post-Export DBPF Magic Verification (`S3PE007`)**:
   - After writing bytes to temporary file, `Sims3PackPayloadExporter` reads the first 4 bytes. They MUST match the `"DBPF"` magic signature (`0x44, 0x42, 0x50, 0x46`). If the magic signature check fails, the temporary file is deleted and controlled error `S3PE007` is returned.

5. **Strict Range & Offset Bounds Validation (`S3PE004`, `S3PE006`)**:
   - Rejects `DataOffset < 0` or `DataOffset >= fileLength` (`S3PE006`).
   - Rejects `EstimatedSizeBytes <= 0` (`S3PE004`).
   - If `EstimatedSizeBytes` is null, exports from `DataOffset` to end of stream.

## Consequences
- Guarantees zero leftover temporary files on disk under any failure or cancellation condition.
- Guarantees source file preservation and atomic target output creation.
- Prevents invalid or non-DBPF payload candidate export attempts.
- Establishes a clean foundation for future application and UI integration workflows (`SIMS-S3P-005`).
