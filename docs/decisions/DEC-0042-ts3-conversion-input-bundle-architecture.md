# DEC-0042: TS3 Canonical Mesh to TS4 Conversion Input Bundle Architecture

- **Status**: Approved
- **Date**: 2026-09-07
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-CONV-006

## Context
In the decorative object conversion workflow, after inspecting source package assets, constructing the source asset graph, decomposing MODL/MLOD containers, and resolving verified TS3 GEOM mesh candidates, an immutable Application-level container model—the **Conversion Input Bundle** (`DecorativeObjectConversionInputBundle`)—is required to bridge the source analysis phase to the target asset generation phase without performing binary package file output.

## Decision

1. **`DecorativeObjectConversionInputBundle` Container Model (`SimsConverter.Application.Models`)**:
   - Immutable record aggregating:
     - `SourcePackagePath`: string
     - `TargetOutputPath`: string
     - `TargetGameVersion`: `GameVersion`
     - `MeshBundles`: `IReadOnlyList<DecorativeObjectMeshInputBundle>` (contains `PackageResourceId`, `FormattedKey`, validated `CanonicalMesh`, and associated LOD/group indices)
     - `TextureAssets`: `IReadOnlyList<DecorativeObjectSourceTextureAsset>`
     - `ObjectModelDecomposition`: `Ts3ObjectModelDecompositionResult?`
     - `RigResources`: `IReadOnlyList<PackageResourceRow>`
     - `RsltResources`: `IReadOnlyList<PackageResourceRow>`
     - `ResourceLinks`: `IReadOnlyList<DecorativeObjectSourceResourceLink>`
     - `IsBundleValid`: bool
     - `Issues`: `IReadOnlyList<ConversionIssue>`

2. **Bundle Builder Boundary (`IDecorativeObjectConversionInputBundleBuilder` / `DecorativeObjectConversionInputBundleBuilder`)**:
   - Converts `DecorativeObjectSourceAssetGraph` and source payloads into `DecorativeObjectConversionInputBundle`.
   - Imports each verified `DecorativeObjectSourceMeshAsset` into a `CanonicalMesh` using `ITs3GeomCanonicalMeshImporter` and validates it with `ICanonicalMeshValidator`.
   - Rejects any mesh candidate failing `CanonicalMesh` import or domain validation (`CONVB001`).

3. **Controlled Diagnostic Diagnostics (`CONVB...`)**:
   - **`CONVB000`**: Null or invalid conversion request/source graph.
   - **`CONVB001`**: Canonical mesh import or domain validation failure for a candidate.
   - **`CONVB002`**: Missing texture candidates in source package.
   - **`CONVB003`**: Missing skeleton/RIG resource (`0x8EAF13DE`) when candidate meshes contain bone weights.
   - **`CONVB004`**: Unverified or missing material reference in bundle.

4. **Governance Guards**:
   - **Zero Speculative Linking**: Automatic speculative material-to-texture links are strictly forbidden (`ResourceLinks` contains only verified links).
   - **Deterministic Sorting**: All collections (`MeshBundles`, `TextureAssets`, `RigResources`, `RsltResources`) are sorted deterministically using ordinal comparison on `FormattedKey`.
   - **No Target Package Output**: TS4 target package writing is not performed during input bundle construction (`STEP-07-TS4-WRITER` remains `NotImplemented`).

## Consequences
- Unblocks Phase 4 conversion pipeline by preparing fully validated, immutable domain input bundles ready for target generation.
- Preserves safety boundaries (no speculative linking, mandatory `CanonicalMesh` validation, zero package writing).
- Ensures 100% build cleanliness (0 errors, 0 warnings) and full test suite passing.
