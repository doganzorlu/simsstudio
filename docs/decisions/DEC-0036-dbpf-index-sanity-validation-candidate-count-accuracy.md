# DEC-0036: DBPF Index Sanity Validation & UI Candidate Count Accuracy Architecture

- **Status**: Approved
- **Date**: 2026-09-02
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-PKG-007

## Context
When inspecting corrupted or malformed DBPF `.package` files, an invalid `indexOffset` (such as `0` or an offset pointing into the 96-byte DBPF header), out-of-bounds resource payload offsets, or misaligned index entries where `TypeId` matches the DBPF header magic (`0x46504244` / `0x44425046`) could cause misclassification or unhandled errors. Additionally, the desktop UI status summary counted all rows (including `Unknown` classification rows) as candidates, distorting candidate summary accuracy.

## Decision

1. **DBPF Parser Index Sanity Guards (`SimsConverter.Package`)**:
   - **Header-Inside Guard**: `DbpfPackageParser` MUST reject `rawIndexOffset == 0` when `indexEntryCount == 0` or any offset pointing inside the DBPF header (`0 < rawIndexOffset < minHeaderSize`, where `minHeaderSize` is 96 for DBPF 2.x and 32 for DBPF 1.x), returning controlled error `PARSE005`. (Note: when `rawIndexOffset == 0` AND `indexEntryCount > 0`, `effectiveIndexOffset` resolves to `96` per DEC-0038).
   - **Resource Payload Bounds Guard**: For every index entry, `entry.DataOffset + entry.CompressedSize <= fileLength` (or `stream.Length`) MUST be verified. Entries exceeding file bounds return controlled error `PARSE015`.
   - **Magic Signature TypeId Guard**: Any index entry where `TypeId` matches `0x46504244` (`"DBPF"` LE) or `0x44425046` (`"DBPF"` BE) is flagged with controlled error `PARSE016`, detecting corrupt or misaligned index offset pointers.

2. **UI Candidate Count Accuracy Policy (`SimsConverter.App`)**:
   - `ResourceInspectorViewModel` status bar message MUST count ONLY actual `KnownTexture` rows for texture candidates and ONLY actual `KnownMesh` rows for mesh candidates.
   - `Unknown` classification rows remain visible in DataGrids for diagnostic inspection, but MUST NOT be counted as candidates in the UI status summary.

## Consequences
- Ensures robust fault tolerance and immediate detection of corrupt DBPF package files before resource extraction or conversion operations.
- Guarantees accurate UI status reporting aligned strictly with verified candidate counts.
- Maintains 100% quality gate compliance across solution projects.
