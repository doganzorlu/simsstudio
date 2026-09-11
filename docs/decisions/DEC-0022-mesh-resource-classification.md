# DEC-0022: Mesh Resource Type Catalog & Candidate Classification Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-001-R2

## Context
Phase 3 of SIMSStudio requires establishing a deterministic mesh and geometry candidate classification foundation (`IMeshResourceClassifier` / `MeshResourceClassifier`) in `SimsConverter.Mesh` to identify 3D model resources, rigs, skeletons, morphs, and slots across Sims 3 and Sims 4 container resources without performing binary buffer parsing, rendering, or file conversion.

## Decision

1. **Verified Mesh TypeId Catalog (`MeshTypeIds.cs`)**:
   - Strictly contains verified TypeId constants sourced from *TheSims4ModdersReference*, *S3PE*, *S4Studio*, and *ModTheSims TS3 PackedFileTypes*:
     - `Ts3Geom` (`0x015A1849`): Sims 3 Geometry Mesh (GEOM).
     - `TsSharedModel` (`0x01661233`): Shared Model Resource (MODL).
     - `TsSharedModelLod` (`0x01D10F34`): Shared Model LOD Resource (MLOD).
     - `TsSharedRig` (`0x8EAF13DE`): Shared Rig / Skeleton Resource (RIG).
     - `TsSharedSlot` (`0xD3044521`): Shared Slot Layout Resource (RSLT).
     - `TsSharedBlendGeometry` (`0x067CAA11`): Shared Blend Geometry / Morph Mesh Resource (BGEO).

2. **Strict Ambiguity & Game Version Policy**:
   - Unambiguous engine-specific TypeIds map automatically to game version (`0x015A1849` [TS3 GEOM]).
   - Ambiguous TypeIds shared across TS3 and TS4 (`0x01661233` [MODL], `0x01D10F34` [MLOD], `0x8EAF13DE` [RIG], `0xD3044521` [RSLT Slot], `0x067CAA11` [BGEO Morph]) MUST NOT guess game version and MUST remain `GameVersion.Unknown` unless an explicit `gameVersionHint` parameter is provided.

3. **Mesh Role Taxonomy (`MeshRoleKind`)**:
   - Defines explicit mesh roles: `Geometry`, `Rig`, `Skeleton`, `Morph`, `Slot`, `Unknown`.

4. **Diagnostic Issue Reporting (`MESHC001`)**:
   - Resources with unrecognized TypeIds return `Classification = Unknown`, `RoleKind = Unknown`, and generate diagnostic warning issue `MESHC001`. Unrecognized resources are never silently ignored.
   - Null or corrupt entries return controlled error issue `MESHC000`.

5. **Zero Binary Buffer Parsing in SIMS-MESH-001**:
   - Performs zero vertex/index buffer reading, binary mesh decoding, 3D preview rendering, or Assimp/SharpGLTF model conversion.

## Consequences
- Establishes Phase 3 Mesh Pipeline architecture foundation aligned with verified modder reference catalogs.
- Guarantees deterministic classification of 3D geometry, rig, morph, and slot candidates without speculative game version guessing.
- Maintains 100% test coverage and quality gate compliance across 6 solution projects.
