# DEC-0041: TS3 Object Model Decomposition GEOM Candidate Resolution Architecture

- **Status**: Approved
- **Date**: 2026-09-07
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-CONV-005 / SIMS-CONV-005-R1

## Context
In TS3 object packages, direct TS3 GEOM (`0x015A1849`) resources are frequently absent from top-level package resource tables, encapsulated instead within `MODL` (`0x01661233`) and `MLOD` (`0x01D10F34`) object model containers. 
To advance from decomposition metadata discovery (SIMS-CONV-004) to active mesh asset candidate selection (SIMS-CONV-005 / R1), an explicit resolution pipeline is required to resolve referenced GEOM resources, enforce strict 5-condition classification validation, verify their presence and canonical mesh importability, reject unverified or mismatched references, and emit controlled diagnostic codes (`CONVG004`, `CONVG005`, `CONVG006`).

## Decision

1. **GEOM Reference Resolution Pipeline (`DecorativeObjectSourceGraphBuilder`)**:
   - Analyzes `ObjectModelDecomposition` metadata results (`ModelMetadataResults`) produced by `ITs3ObjectModelDecompositionService`.
   - Iterates through `GeometryReferences` and `LodInfos` to discover candidate TS3 GEOM (`0x015A1849`) and model references.
   - Enforces package index presence and strict 5-condition classification validation before accepting any reference as a `DecorativeObjectSourceMeshAsset`.

2. **Strict 5-Condition Candidate Acceptance Guard (R1 Hardening)**:
   A decomposition-resolved GEOM candidate MUST satisfy ALL 5 mandatory conditions to be accepted into `MeshAssets`:
   1. `ClassificationKind == MeshClassificationKind.KnownMesh`
   2. `DetectedGameVersion == GameVersion.Sims3`
   3. `RoleKind == MeshRoleKind.Geometry`
   4. `Entry.Id.TypeId == 0x015A1849` (TS3 GEOM)
   5. `CanInspectCanonicalMesh == true`

3. **Unverified Reference Guard & Diagnostic Codes**:
   - **`CONVG004` (Missing Reference)**: Generated when a referenced GEOM or model `PackageResourceId` is not present in the package resource index (`packageInspection.Resources`).
   - **`CONVG005` (Incompatible/Failed Import/Mismatched Reference)**: Generated when a referenced GEOM resource exists in the package but fails any of the 5 mandatory classification/import conditions (e.g. TS4 GEOM, UnknownMesh classification, non-Geometry role, mismatched TypeId, or `CanInspectCanonicalMesh == false`).
   - **`CONVG006` (Cyclic Reference)**: Generated when circular model-to-model references (e.g. MODL -> MLOD -> MODL) are detected during resolution traversal.
   - Unverified heuristic, mismatched, or corrupted references are strictly rejected and NEVER added to `MeshAssets`.

4. **Canonical Mesh Pipeline Integration & Asset Graph Readiness**:
   - Verified GEOM resources are converted into `DecorativeObjectSourceMeshAsset` entries and added to `MeshAssets`.
   - `HasImportableMesh` evaluates to `true` when at least one verified `DecorativeObjectSourceMeshAsset` is produced.
   - `IsSourceGraphReady` evaluates to `true` when `packageInspection.IsSuccess && HasImportableMesh`.
   - `CONVG003` is emitted only when `HasImportableMesh` remains `false`.

5. **Deterministic Candidate Ordering & Zero Speculative Linking**:
   - Resolved GEOM candidates in `MeshAssets` are sorted deterministically using `FormattedKey` (ordinal comparison).
   - Automatic speculative mesh-to-texture links remain strictly forbidden (`ResourceLinks` remains empty).

## Consequences
- Prevents misclassified or TS4-origin meshes from being erroneously ingested as TS3 canonical mesh candidates during MODL/MLOD decomposition resolution.
- Preserves strict safety guarantees (no unverified candidates, zero speculative linking, deterministic sorting).
- Maintains 100% build cleanliness (0 errors, 0 warnings) and full test suite passing.
