# DEC-0007: DBPF Resource Inspector UI Screen Architecture

- **Status**: Approved
- **Date**: 2026-08-27
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-PKG-005

## Context
SIMSStudio requires a fully functional Avalonia UI presentation screen for inspecting DBPF package containers. The screen must allow file selection (via text input or file picker dialog), display resource entries in a monospaced data table, report diagnostic parser issues transparently, and operate asynchronously without blocking the UI thread or performing low-level binary byte parsing in presentation code.

## Decision

1. **File Selection & Abstraction (`IFilePickerService`)**:
   - Introduced `IFilePickerService` and `AvaloniaFilePickerService` utilizing Avalonia `TopLevel.StorageProvider.OpenFilePickerAsync` with `*.package` filter.
   - Decoupled ViewModel from direct Avalonia UI control code-behind to preserve 100% unit testability in `SimsConverter.App.Tests`.

2. **Resource Index Table (`DataGrid`)**:
   - Implemented `DataGrid` (bound to `Resources`) displaying `FormattedKey`, `TypeHex`, `GroupHex`, `InstanceHex`, `Offset`, `CompressedSize`, `DecompressedSize`, and `CompressionName`.
   - Monospaced formatting configured for hexadecimal key columns to ensure vertical character alignment across table rows.

3. **Transparent Diagnostic Issue Reporting**:
   - Diagnostic issues (`ConversionIssue`) returned by `IPackageInspectionService` are displayed in a dedicated issue border section (`ListBox` bound to `Issues`), exposing error codes, severity levels, and detailed messages.

4. **UI State Management**:
   - Supports 5 explicit UI states: No file selected, Loading (`IsBusy == true`), Empty package container (0 resources found), Inspection Success (`N` resources displayed in DataGrid), and Diagnostic Error state.

5. **Strict Binary Isolation**:
   - Verified via `SourceScan_AppSourceFilesShouldNotContainBinaryPrimitivesOrBinaryParsing` test that `SimsConverter.App` contains ZERO binary byte offset parsing or `System.Buffers.Binary` references.

## Consequences
- Developers can inspect any DBPF package file interactively in Avalonia UI.
- Prepares the presentation layer for future asset preview and conversion features (EPIC-004) without refactoring the inspection UI boundary.
