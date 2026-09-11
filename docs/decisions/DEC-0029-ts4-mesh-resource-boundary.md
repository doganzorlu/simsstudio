# DEC-0029: TS4 Mesh Resource Format Research & Type Boundary Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-008

## Context
Phase 3 of SIMSStudio requires establishing an authoritative research foundation and strict type boundary for *The Sims 4* (TS4) 3D mesh resources prior to developing binary parsers or mesh importers. This boundary ensures that only grounded, verified TypeIds are recognized as production mesh constants, while unverified candidate TypeIds are handled cleanly with diagnostic warning issues (`MESHC001`).

## Decision

1. **Grounded TS4 TypeId Catalog (`SimsConverter.Mesh.Constants.MeshTypeIds`)**:
   - Every production constant in `MeshTypeIds.cs` MUST be verified against authoritative primary/secondary documentation sources:
     - `0x015A1849` (GEOM / Geometry): Confirmed CAS Parts geometry resource (Shared TS3/TS4). Non-breaking alias `TsSharedGeom` provided.
     - `0x01661233` (MODL / Model): Confirmed TS4 Object Model geometry resource (`TsSharedModel`).
     - `0x01D10F34` (MLOD / Model LOD): Confirmed TS4 Object Level-of-Detail geometry resource (`TsSharedModelLod`).
     - `0x8EAF13DE` (RIG / Skeleton): Confirmed TS4 Joint Hierarchy / Skeleton resource (`TsSharedRig`).
     - `0xD3044521` (RSLT / Slot): Confirmed TS4 Slot Layout resource (`TsSharedSlot`).
     - `0x067CAA11` (BGEO / Blend Geometry): Confirmed TS4 Blend Geometry morph slider resource (`TsSharedBlendGeometry`).

2. **Strict Guard Against Guessed / Unverified TypeIds**:
   - Unverified TypeId candidates (e.g. `0x025C6425`, `0x02864C99`) MUST NOT be registered as production constants in `MeshTypeIds.cs` without sample package verification.
   - Unrecognized TypeIds produce a controlled diagnostic warning issue (`MESHC001`) and fall back to `Classification = Unknown`.
   - Enforced by automated type boundary guard tests (`Ts4MeshResourceClassifierTests`).

3. **Classifier Game Version Hint Integration (`MeshResourceClassifier`)**:
   - `MeshResourceClassifier.Classify` evaluates `GameVersion gameVersionHint`.
   - `0x015A1849` (GEOM / `TsSharedGeom`) with `gameVersionHint == GameVersion.Sims4` returns `DetectedGameVersion = GameVersion.Sims4` and `FormatName = "TS4 Geometry (GEOM)"`.
   - `0x015A1849` (GEOM) without TS4 hint defaults cleanly to `DetectedGameVersion = GameVersion.Sims3` and `FormatName = "TS3 Geometry (GEOM)"` (zero regression for TS3 pipeline).
   - `0x01661233` (MODL) and `0x01D10F34` (MLOD) return `DetectedGameVersion = GameVersion.Sims4` and format names `"TS4 Object Model (MODL)"` / `"TS4 Object Model LOD (MLOD)"` when `gameVersionHint == GameVersion.Sims4`.

4. **Recommendation for TS4 Mesh Importer Entry Point**:
   - **Recommended Entry Point (Option A)**: **TS4 CAS GEOM (`0x015A1849`) Importer**, extending the candidate-validated RCOL chunk slice importer established in `SIMS-MESH-005-R1`.

## Consequences
- Establishes a verified, non-speculative foundation for TS4 mesh pipeline development.
- Preserves existing decision records (`DEC-0024`, `DEC-0025`, `DEC-0026`, `DEC-0027`, `DEC-0028` status Approved).
- Maintains quality gate compliance across solution projects.
