# DEC-0028: Mesh Inspection UI Integration Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-007

## Context
Phase 3 of SIMSStudio requires connecting the Application layer mesh inspection boundary service (`IMeshInspectionService` / `MeshInspectionService`) and `MeshResourceRow` presentation models into the Avalonia UI MVVM presentation layer (`ResourceInspectorViewModel` and `MainWindow.axaml`).

## Decision

1. **ViewModel State Integration (`ResourceInspectorViewModel`)**:
   - Injects `IMeshInspectionService? meshInspectionService`.
   - Exposes `ObservableCollection<MeshResourceRow> MeshResources`, `MeshResourceRow? SelectedMeshResource`, `HasMeshResources`, and `CanInspectSelectedMesh`.
   - Populates `MeshResources` automatically during package inspection workflows (non-Sims3Pack mode).
   - Clears `MeshResources` when package inspection fails or when Sims3Pack mode is active.
   - Status text updated to report resource entries, texture candidate counts, and mesh candidate counts (`Found X resource entries (Y texture candidates, Z mesh candidates)`).

2. **UI DataGrid Layout Integration (`MainWindow.axaml`)**:
   - Adds a dedicated `TabItem` ("Mesh Candidates") inside `<TabControl IsVisible="{Binding !IsSims3PackMode}">`.
   - DataGrid displays 13 columns:
     - `FormattedKey` (`monospaced`)
     - `FormatName`
     - `RoleKind`
     - `DetectedGameVersion`
     - `CanExtractRawPayload`
     - `CanInspectCanonicalMesh`
     - `VertexCount`
     - `FaceCount`
     - `BoneCount`
     - `HasNormals`
     - `HasUv0`
     - `HasBoneWeights`
     - `Issues.Count`

3. **Strict UI Boundary & Selection Behavior Rules**:
   - Zero binary parsing, byte offset manipulation, `System.Buffers.Binary.BinaryPrimitives`, or GEOM chunk decoding is executed in `SimsConverter.App`.
   - Avalonia UI components bind strictly to `IMeshInspectionService` output models (`MeshResourceRow`).
   - Selecting a valid TS3 GEOM mesh row updates `SelectedMeshResource` and evaluates `CanInspectSelectedMesh == true`.
   - Mesh summary fields (`VertexCount`, `FaceCount`, `BoneCount`, `HasNormals`, `HasUv0`, `HasBoneWeights`, `ValidationIssueCount`) projected from lower layers are accurately presented in UI row models.
   - Enforced by automated UI assembly source scan guard test (`AppAssembly_ContainsNoProhibitedBinaryParsingReferences`) and deterministic ViewModel unit tests.

## Consequences
- Provides a clean, responsive desktop UI interface for inspecting 3D mesh candidates and viewing TS3 GEOM canonical mesh summaries.
- Preserves existing decision records (`DEC-0024`, `DEC-0025`, `DEC-0026`, `DEC-0027` status Approved).
- Maintains quality gate compliance across solution projects.
