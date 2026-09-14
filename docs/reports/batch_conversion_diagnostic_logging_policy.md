# Batch Conversion Diagnostic Logging & Path Redaction Policy

## Overview
`SIMS-CONV-BATCH-003` establishes a comprehensive diagnostic logging and export architecture for folder-based batch conversion operations in SIMSStudio.

For every batch conversion run, diagnostic logs are generated in both human-readable text format (`.log`) and machine-parseable JSON format (`.json`).

---

## Log Output Specification

- **Default Path Pattern:**
  - `artifacts/logs/batch-conversion-<timestamp>-<runId>.json`
  - `artifacts/logs/batch-conversion-<timestamp>-<runId>.log`
- **Timestamp Format:** `yyyyMMdd-HHmmss` (UTC)
- **RunId Format:** Unique 32-character hexadecimal GUID (e.g. `8f3a1b2c4d5e6f7a8b9c0d1e2f3a4b5c`)

---

## Data Schema & Fields Logged

1. **Batch Session Metadata:**
   - `RunId`: Unique execution identifier.
   - `StartTimeUtc`, `EndTimeUtc`, `TotalDurationMs`: High-precision timing metrics.
   - `SourceFolderPath`, `OutputFolderPath`: Normalized source and target directories.
   - `Summary`: Aggregate file counts (Total, Success, Failed, Skipped, Ignored).

2. **Pipeline Phases Logged:**
   - `Discovery`: Directory scanning and standalone file identification.
   - `PackageInspection`: DBPF/Sims3Pack header parsing and entry index reading.
   - `Classification`: Main category determination (Decorative Object, CAS Part, Unknown).
   - `ConversionRouting`: Pipeline selection based on game version and item category.
   - `PayloadConversion`: Mesh, texture, CASP, and rig remapping conversions.
   - `PackageWrite`: Target DBPF package serialization.
   - `PostWriteValidation`: Output resource graph and identity verification.
   - `AtomicCommit` / `AtomicRollback`: Staging file commit or failure rollback execution.

3. **Per-File Diagnostic Item Breakdown:**
   - Source & Target File Paths, Extension, Game Versions, Conversion Direction.
   - Status (`Success`, `Failed`, `Skipped`, `Ignored`).
   - Produced Resource Count & Metrics (Vertex Count, Face Count, Texture Count, CASP TypeId).
   - Diagnostic Issue List (Code, Severity, Message, Target Resource).
   - Exception Breakdown (Exception Type, Message, Full StackTrace).

---

## Path Redaction & Privacy Policy

1. **Path Normalization:** All file paths logged use standard forward slashes (`/`) for cross-platform log consistency across macOS and Windows.
2. **Sensitive Query Parameter Redaction:** Any file paths containing embedded credentials, secrets, or authentication query parameters (e.g. `password=`, `secret=`, `token=`) are automatically sanitized to `?[REDACTED_QUERY_PARAMS]`.
3. **User Home Path Context:** Source and output paths within the user directory structure are preserved to maintain diagnostic traceability for the end-user while stripping ephemeral query tokens.

---

## Error Isolation & Non-Blocking Guarantee

Diagnostic logging operations are strictly isolated from the conversion execution pipeline:
- Writing log files is wrapped in non-throwing handlers.
- A failure to create, format, or write diagnostic log files (e.g., due to disk space or read-only filesystem restrictions) will **NEVER** fail, crash, or interrupt the batch conversion of package containers.
