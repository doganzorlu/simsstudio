# DEC-0027: Mesh Inspection Application Boundary Service Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-006

## Context
Phase 3 of SIMSStudio requires establishing an Application layer boundary service (`IMeshInspectionService` / `MeshInspectionService`) in `SimsConverter.Application` to orchestrate package resource inspection, mesh candidate classification (`IMeshResourceClassifier`), and TS3 GEOM canonical mesh decoding (`ITs3GeomCanonicalMeshImporter`) into presentation-ready UI models (`MeshResourceRow`, `MeshInspectionResult`).

## Decision

1. **Application Boundary & Contract Definition (`SimsConverter.Application`)**:
   - Contract: `IMeshInspectionService` (`InspectPackageMeshesAsync`, `InspectPackageMeshes`).
   - Implementation: `MeshInspectionService` injecting `IPackageInspectionService`, `IMeshResourceClassifier`, `ITs3GeomCanonicalMeshImporter`, and `IPackageResourcePayloadReader`.
   - Result Model: `MeshInspectionResult` (`IsSuccess`, `PackageFilePath`, `IReadOnlyList<MeshResourceRow> Rows`, `IReadOnlyList<ConversionIssue> Issues`).

2. **Presentation Model Policy (`MeshResourceRow`)**:
   - Exposes formatted hex keys (`FormattedKey`, `TypeHex`, `GroupHex`, `InstanceHex`), offsets, sizes, classification kinds (`MeshClassificationKind`), roles (`MeshRoleKind`), game versions (`DetectedGameVersion`), format names (`FormatName`), capabilities (`CanExtractRawPayload`, `CanInspectCanonicalMesh`), and canonical mesh decoding summary fields (`VertexCount`, `FaceCount`, `BoneCount`, `HasNormals`, `HasUv0`, `HasBoneWeights`, `ValidationIssueCount`).

3. **Strict Architecture & Payload Reading Boundaries**:
   - **Zero** `File.ReadAllBytes`, low-level binary offset parsing, byte primitives (`BinaryPrimitives`), stream slicing (`AsSpan`, `Slice`), or GEOM chunk scanning is permitted in `SimsConverter.Application`. Enforced by automated source scan guard test (`ApplicationBoundaryGuardTests`).
   - All raw package resource payload reading is encapsulated behind `IPackageResourcePayloadReader` / `PackageResourcePayloadReader` in `SimsConverter.Package`.
   - All classification logic is delegated to `IMeshResourceClassifier` in `SimsConverter.Mesh`.
   - All binary GEOM decoding and canonical mesh validation are delegated to `ITs3GeomCanonicalMeshImporter` in `SimsConverter.Mesh`.

4. **Fault Tolerance & Diagnostic Issue Handling**:
   - Package file missing, payload out-of-bounds, or read failure errors returned by `IPackageResourcePayloadReader` are cleanly attached as row issues (`row.Issues`).
   - Unknown mesh resources are retained in `Rows` with `ClassificationKind = Unknown` and a diagnostic warning issue (`MESHC001`).
   - Importer failures (e.g. truncated or corrupted GEOM payload) capture diagnostic issues in `row.Issues` and set `CanInspectCanonicalMesh = false` without throwing unhandled exceptions or crashing the application.
   - Null or empty input paths return controlled failure results (`MESHA000`).

## Consequences
- Provides a clean, strictly compliant UI-ready application boundary for Avalonia UI DataGrid binding in future tasks.
- Preserves existing decision records (`DEC-0024`, `DEC-0025`, `DEC-0026` status Approved).
- Maintains quality gate compliance across solution projects.
