# DEC-0024: Canonical Mesh Domain Model Foundation Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-003-R2

## Context
Phase 3 of SIMSStudio requires establishing a game-agnostic `CanonicalMesh` domain model and validation foundation (`ICanonicalMeshValidator` / `CanonicalMeshValidator`) in `SimsConverter.Domain` to represent 3D meshes, vertices, triangles, material slots, bone weights, and coordinate systems independently of engine-specific binary formats (`GEOM`, `MODL`, `MLOD`, `BGEO`).

## Decision

1. **Game-Agnostic Canonical Mesh Domain Types & Init-Setter Immutability Hardening (`SimsConverter.Domain`)**:
   - `CanonicalMesh`: Record containing `Name`, `Vertices`, `Faces`, `Materials`, `CoordinateSystem`, `SourceGameVersion`, and `Issues`.
     - **Get-Only Collection Properties**: All collection properties (`Vertices`, `Faces`, `Materials`, `Issues`) are defined as `{ get; }` without `init` or `set` accessors. This prevents object initializer or `with` expression bypasses (`mesh with { Vertices = mutableList }`).
     - **Defensive Copy Constructor**: The constructor performs defensive copies (`Array.AsReadOnly(items.ToArray())`) on all incoming collection parameters, ensuring external caller list mutations cannot alter initialized mesh state.
   - `CanonicalVertex`: Vertex representation (`Position`, `Normal?`, `Tangent?`, `Uv0?`, `Uv1?`, `BoneWeights?`).
     - **Get-Only Properties & Defensive Copying**: All properties including `BoneWeights` are `{ get; }` only, with defensive copying performed on `BoneWeights` (`Array.AsReadOnly(boneWeights.ToArray())`), insulating vertex bone bindings from object initializer or `with` expression bypasses.
   - `CanonicalFace`: Value struct representing triangle vertex indices (`A`, `B`, `C`).
   - `CanonicalMaterialSlot`: Material binding (`SlotIndex`, `MaterialName`).
   - `CanonicalBoneWeight`: Bone weight binding (`BoneIndex`, `Weight`).
   - Value Structs: `MeshVector2`, `MeshVector3`, `MeshVector4`.
   - Enum: `CanonicalCoordinateSystem` (`RightHandedYUp`, `RightHandedZUp`, `LeftHandedYUp`, `LeftHandedZUp`).

2. **Validation Service & Error Codes (`ICanonicalMeshValidator`)**:
   - `CanonicalMeshValidationResult Validate(CanonicalMesh mesh)`
   - Error & Warning Rules:
     - `MESHV000`: `mesh == null` -> Controlled Error.
     - `MESHV001`: `Vertices` list is empty -> Controlled Error.
     - `MESHV002`: `Faces` list is empty -> Controlled Error.
     - `MESHV003`: Face index out of range (`A`, `B`, or `C` $< 0$ or $\ge$ vertex count) -> Controlled Error.
     - `MESHV004`: Degenerate triangle (`A == B`, `B == C`, or `A == C`) -> Warning.
     - `MESHV005`: Invalid vertex position (`float.IsNaN` or `float.IsInfinity`) -> Controlled Error.
     - `MESHV006`: Bone weight sum outside tolerance $[0.99, 1.01]$ or negative bone index -> Warning.

3. **Zero External Dependencies & Zero 3D Binary Parsing**:
   - `SimsConverter.Domain` maintains zero external 3D library dependencies (no Assimp, SharpGLTF, etc.).
   - SIMS-MESH-003-R2 performs zero binary decoding, GEOM/MODL structure parsing, 3D preview rendering, or coordinate system transforms.

## Consequences
- Establishes a pure, 100% immutable intermediate representation for 3D meshes in SIMSStudio.
- Prevents init-setter and `with` expression bypasses of defensive copy collection snapshots.
- Maintains 100% test coverage and quality gate compliance across 6 solution projects.
