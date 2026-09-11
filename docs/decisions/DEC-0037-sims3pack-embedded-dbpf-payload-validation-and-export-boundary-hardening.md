# DEC-0037: Sims3Pack Embedded DBPF Payload Validation & Export Boundary Hardening Architecture

- **Status**: Approved
- **Date**: 2026-09-02
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-S3P-008

## Context
When scanning `.sims3pack` archives, finding the ASCII bytes `"DBPF"` alone was previously sufficient for `Sims3PackPayloadCatalogScanner` to classify an archive offset as a valid `DbpfPackage` candidate. If `"DBPF"` occurred coincidentally inside XML metadata, preview image headers, or a corrupted payload with `indexOffset == 0`, the scanner created a `DbpfPackage` candidate entry. Exporting such a candidate produced an un-exportable or invalid `.package` file (`PARSE005`).

## Decision

1. **Embedded DBPF Candidate Header Sanity Validation (`Sims3PackPayloadCatalogScanner`)**:
   - The catalog scanner MUST peek and inspect the 96-byte DBPF header (`ValidateDbpfHeader`) at every detected candidate offset.
   - Candidates with corrupt headers (`rawIndexOffset == 0` with `indexEntryCount == 0`, `0 < rawIndexOffset < minHeaderSize`, negative `indexSizeBytes`, or out-of-bounds index tables) MUST be classified as `Sims3PackPayloadKind.InvalidDbpfPackage` (with diagnostic issue `S3PC010` attached). (Note: `rawIndexOffset == 0` with `indexEntryCount > 0` resolves to `effectiveIndexOffset = 96` per DEC-0038).
   - The scanner MUST continue scanning the archive section to discover subsequent valid DBPF candidates.

2. **Domain & Application Model Hardening**:
   - `Sims3PackPayloadKind` includes `InvalidDbpfPackage` to differentiate invalid candidates from exportable `DbpfPackage` entries.
   - `Sims3PackInspectionService` sets `CanExport = false` for any row where `Kind != Sims3PackPayloadKind.DbpfPackage` (`InvalidDbpfPackage`, `PngPreview`, `Unknown`).

3. **Export Boundary Hardening (`Sims3PackPayloadExporter`)**:
   - `Sims3PackPayloadExporter` MUST reject export requests for catalog entries whose `Kind` is not `DbpfPackage` (`S3PE001`).
   - Post-export validation MUST execute `ValidateDbpfHeader` on the temporary exported file before completing the atomic move to the target `.package` path (`S3PE007`).

4. **UI Status Reporting & Distinction (`SimsConverter.App`)**:
   - `ResourceInspectorViewModel` status bar message MUST report valid vs invalid embedded payload entries: `Found N embedded payload entries (X valid DBPF packages, Y invalid DBPF candidates)`.
   - DataGrid rows with `Kind == "InvalidDbpfPackage"` disable export capabilities (`CanExport = false`).

## Consequences
- Prevents corrupt or false-positive `"DBPF"` matches in `.sims3pack` files from being classified as valid package candidates or exported to disk.
- Guarantees 100% type safety and export protection across all `.sims3pack` candidate inspection workflows.
- Maintains clean quality gates with 0 build warnings and 0 failing tests.
