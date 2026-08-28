# DEC-0013: Sims3Pack Application Boundary Service Architecture

- **Status**: Pending Approval
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-S3P-005

## Context
SIMSStudio requires an Application layer use-case boundary service (`ISims3PackInspectionService` / `Sims3PackInspectionService`) to orchestrate container detection, XML metadata parsing, payload catalog scanning, and embedded DBPF payload exporting without exposing binary or XML primitives to the presentation/UI layer.

## Decision

1. **Single Application Boundary Use-Case Orchestration**:
   - `Sims3PackInspectionService` unifies `ISims3PackDetector`, `ISims3PackXmlParser`, `ISims3PackPayloadCatalogScanner`, and `ISims3PackPayloadExporter` into higher-level use-case methods (`InspectFileAsync` and `ExportPayloadAsync`).

2. **Controlled Issue Aggregation Policy**:
   - Diagnostic issues from container detection, XML parsing, catalog scanning, and payload export are aggregated into `Sims3PackInspectionResult.Issues` or `Sims3PackExportResult.Issues`.
   - **Partial Success Handling**:
     - Container detection failure: Aborts immediately with `S3PA001` error.
     - XML metadata parse failure: Aborts immediately with `S3PA002` error **WITHOUT** calling the catalog scanner.
     - Empty archive catalog: Returns successful inspection result (`IsSuccess == true`) with empty payload rows list (`PayloadRows.Count == 0`).

3. **Presentation-Ready Application Models (`Sims3PackPayloadRow`)**:
   - Domain catalog entries are projected into `Sims3PackPayloadRow` models for UI consumption.
   - `DataOffsetHex` formatted as `$"0x{entry.DataOffset:X8}"`.
   - `EstimatedSizeFormatted` formatted in bytes / KB / MB.
   - `CanExport` set to `true` strictly when `Kind == Sims3PackPayloadKind.DbpfPackage`. Non-DBPF candidates (`PngPreview`, `Unknown`) have `CanExport == false`.

4. **Export Delegation & Target Filename Sanitization**:
   - `ExportPayloadAsync` validates that `SelectedRow.CanExport == true`. Non-DBPF export requests are rejected with `S3PA005`.
   - Generates sanitized target output filenames using `Regex` to replace illegal path characters with `_` and enforces `.package` extension.
   - Delegates raw binary byte range extraction to `ISims3PackPayloadExporter` in the Package layer, preserving all package-level path comparison (`S3PE008`), overwrite protection (`S3PE005`), post-export DBPF header validation (`S3PE007`), and atomic temp file cleanup rules.

## Consequences
- Prevents Avalonia UI and ViewModel components from depending on XML readers, byte streams, or binaryPrimitives.
- Ensures consistent diagnostic issue reporting across all Sims3Pack workflow steps.
- Establishes a clean foundation for upcoming UI ViewModel integration tasks (`SIMS-S3P-006`).
