# DEC-0021: Texture Inspection UI Integration Architecture

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-TEX-006

## Context
Phase 2 of SIMSStudio requires presenting texture candidate classification and capability analysis in the Avalonia Desktop UI (`ResourceInspectorViewModel` and `MainWindow.axaml`) when a `.package` file is inspected or when an embedded DBPF payload is extracted from a `.sims3pack` container.

## Decision

1. **ViewModel Texture Inspection State Integration**:
   - `ResourceInspectorViewModel` encapsulates texture inspection state via:
     - `ObservableCollection<TextureResourceRow> TextureResources`
     - `TextureResourceRow? SelectedTextureResource`
     - `bool HasTextureResources` (`TextureResources.Count > 0`)
     - `bool CanExtractSelectedTexture` (`SelectedTextureResource?.CanExtractRawPayload == true`)
     - `bool CanParseSelectedDdsHeader` (`SelectedTextureResource?.CanParseDdsHeader == true`)

2. **Tabbed UI Presentation in `MainWindow.axaml`**:
   - Organized package resource inspection into a `TabControl` containing two tabs when in Package mode (`IsSims3PackMode` is false):
     - **Package Resources Tab**: Raw DBPF entry table (`Resources`).
     - **Texture Candidates Tab**: Texture classification table (`TextureResources`) exposing `FormattedKey` (monospaced), `FormatName`, `MapKind`, `DetectedGameVersion`, `CanExtractRawPayload`, `CanParseDdsHeader`, and issue count.

3. **Non-Destructive & Zero Binary Parsing in UI**:
   - UI layer performs zero low-level binary byte parsing, stream manipulation, or DDS header decoding. All classification data is produced by `ITextureInspectionService`.

4. **Preservation of Unknown Resources & Diagnostic Issues**:
   - Resources with unrecognized TypeIds are rendered in the Texture Candidates table as `ClassificationKind = Unknown` with issue count reflecting diagnostic uyarısı `TEXC001`.

## Consequences
- Provides users with an intuitive visual catalog of texture candidates, map roles, game versions, and extraction/DDS capabilities in SIMSStudio Desktop.
- Maintains 100% test coverage and quality gate compliance across 5 solution projects.
