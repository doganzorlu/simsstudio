# DEC-0014: Sims3Pack UI Inspection Screen Architecture

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-S3P-006-R1

## Context
SIMSStudio requires Avalonia UI integration for inspecting `.sims3pack` containers, displaying manifest metadata and embedded payload entries in a presentation table, and allowing users to select `.sims3pack` files as first-class container files in system file dialogs and trigger raw `.package` payload exports, while preserving UI Contract governance and preventing low-level binary/XML parsing inside presentation components.

## Decision

1. **First-Class File Dialog Filtering (`AvaloniaFilePickerService`)**:
   - `AvaloniaFilePickerService.OpenPackageFilePickerAsync` configures `.sims3pack` as a first-class file extension filter.
   - Configured filter options:
     1. `Sims Containers (*.package; *.sims3pack)` (Default combined filter)
     2. `DBPF Package (*.package)`
     3. `Sims3Pack Container (*.sims3pack)`
     4. `All Files`
   - File picker dialog title updated to `"Select Sims Package or Sims3Pack File"`.

2. **Zero Binary & XML Primitives in Presentation Layer**:
   - `SimsConverter.App` contains ZERO binary parsing primitives (`BinaryPrimitives`, `FileStream`, `System.Buffers.Binary`, `System.Xml`).
   - The ViewModel communicates strictly with `ISims3PackInspectionService` in `SimsConverter.Application`.

3. **Automatic Workflow Switching in `ResourceInspectorViewModel`**:
   - When a `.sims3pack` file path is selected and inspected, `ResourceInspectorViewModel` sets `IsSims3PackMode = true` and invokes `ISims3PackInspectionService.InspectFileAsync(filePath)`.
   - When a DBPF `.package` file is selected, `IsSims3PackMode = false` is set and standard DBPF inspection executes. Existing DBPF inspection/export workflows remain 100% unchanged.

4. **Presentation Table & Export Action Guard**:
   - Sims3Pack container metadata (Title, Asset ID, Asset Type, Root Element, Encoding, Raw XML Size) is presented in a dedicated metadata panel.
   - Embedded payload candidates are populated in `Sims3PackPayloads` DataGrid with columns: `#` (`EntryIndex`), `Kind`, `Offset` (`DataOffsetHex`, `monospaced`), `Estimated Size` (`EstimatedSizeFormatted`), `Display Name` (`DisplayName`), `Exportable` (`CanExport`), and `Issues` count.
   - `ExportSims3PackPayloadCommand` is enabled strictly when `CanExportSims3PackPayload` is true (`IsSims3PackMode == true`, `SelectedSims3PackPayload != null`, and `SelectedSims3PackPayload.CanExport == true` [DBPF candidates only]). Non-DBPF rows (`PngPreview`, `Unknown`) cannot be exported.

5. **UI Contract & Semantic XAML Styling**:
   - All colors and borders in [MainWindow.axaml](../../src/SimsConverter.App/MainWindow.axaml) use semantic `DynamicResource` references. Zero inline hex color codes are used.
   - Hex offset columns specify `CellStyleClasses="monospaced"`.

## Consequences
- Enables seamless browsing, inspection, and payload export of `.sims3pack` files directly within SIMSStudio UI file dialogs.
- Enforces strict layer separation and UI Contract compliance.
- All unit tests and static code analysis scans in `SimsConverter.App.Tests` pass with zero warnings or errors.
