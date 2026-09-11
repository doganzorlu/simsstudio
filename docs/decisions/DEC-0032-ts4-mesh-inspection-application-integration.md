# DEC-0032: TS4 Canonical Mesh Inspection Application Integration Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-011

## Context
Phase 3 of SIMSStudio requires integrating `ITs4GeomCanonicalMeshImporter` into `MeshInspectionService` in `SimsConverter.Application` to decode *The Sims 4* (TS4) CAS GEOM (`0x015A1849` / `TsSharedGeom`) binary package entries into canonical mesh entities and project their decoding summaries to application presentation models (`MeshResourceRow`).

## Decision

1. **Application Service Integration (`SimsConverter.Application`)**:
   - `MeshInspectionService` receives `ITs4GeomCanonicalMeshImporter` in its constructor via Dependency Injection.
   - Resource Classification & Dispatch Policy:
     - If `row.Classification.Kind == KnownMesh` AND `row.ResourceKey.TypeId == 0x015A1849`:
       - If `row.Classification.DetectedGameVersion == GameVersion.Sims4`: Invokes `_ts4GeomImporter.Import(payload, entry.Id.FormattedKey)`.
       - If `row.Classification.DetectedGameVersion == GameVersion.Sims3` (or default): Invokes `_ts3GeomImporter.Import(payload, entry.Id.FormattedKey)`.
   - Populates `MeshResourceRow` canonical summary properties for both TS3 and TS4 GEOMs:
     - `CanInspectCanonicalMesh`
     - `VertexCount`
     - `FaceCount`
     - `BoneCount`
     - `HasNormals`
     - `HasUv0`
     - `HasBoneWeights`
     - `ValidationIssueCount`

2. **Fault Tolerance & App Boundary Safety**:
   - Importer failure or corrupted GEOM payload does **not** crash the application or omit the row.
   - On importer failure: `CanInspectCanonicalMesh = false`, row remains visible, and diagnostic issues are attached to `meshRow.Issues`.
   - **Zero** low-level binary parsing, `File.ReadAllBytes`, `AsSpan`, `Slice`, or manual byte decoding is performed in `SimsConverter.Application`. Payload reading delegates strictly to `IPackageResourcePayloadReader`.

3. **TS3 GEOM Reference Protection**:
   - Existing TS3 GEOM inspection pipeline remains untouched and continues to delegate to `ITs3GeomCanonicalMeshImporter`.

## Consequences
- Establishes a unified mesh inspection pipeline in `SimsConverter.Application` capable of decoding both TS3 and TS4 CAS GEOM resources into canonical mesh entities.
- Preserves existing decision records (`DEC-0024`, `DEC-0025`, `DEC-0026`, `DEC-0027`, `DEC-0028`, `DEC-0029`, `DEC-0030`, `DEC-0031` status Approved).
- Maintains quality gate compliance across solution projects.
