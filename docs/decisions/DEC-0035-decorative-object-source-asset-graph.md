# DEC-0035: TS3 Decorative Object Source Asset Graph Builder Architecture

- **Status**: Approved
- **Date**: 2026-09-02
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-CONV-002 / SIMS-CONV-002-R1 / SIMS-CONV-002-R2

## Context
Phase 4 of SIMSStudio requires building a deterministic source asset graph (`DecorativeObjectSourceAssetGraph`) for TS3 decorative object packages before conversion. To ensure robust asset discovery without creating false positive linkages or attempting premature TS4 package writing, an Application layer builder service (`IDecorativeObjectSourceGraphBuilder` / `DecorativeObjectSourceGraphBuilder`) is established to collect mesh assets (`DecorativeObjectSourceMeshAsset`), texture assets (`DecorativeObjectSourceTextureAsset`), and resource links (`DecorativeObjectSourceResourceLink`).

## Decision

1. **Application Boundary & Service Definition (`SimsConverter.Application`)**:
   - Contract: `IDecorativeObjectSourceGraphBuilder` (`BuildGraphAsync`, `BuildGraph`).
   - Implementation: `DecorativeObjectSourceGraphBuilder` injecting `IPackageInspectionService`, `IMeshInspectionService`, and `ITextureInspectionService`.
   - Domain/Application Models: `DecorativeObjectSourceAssetGraph`, `DecorativeObjectSourceMeshAsset` (includes `DetectedGameVersion`), `DecorativeObjectSourceTextureAsset`, `DecorativeObjectSourceResourceLink`, `DecorativeObjectAssetRole`.

2. **Graph Construction & Strict Feasibility Policy**:
   - **No Speculative Mesh-to-Texture Linking**: Automatic all-to-all `MeshToTexture` linking is removed. `ResourceLinks` MUST remain empty unless explicit verified TGI material references exist.
   - **Strict `HasImportableMesh` Criteria**: `HasImportableMesh` MUST be `true` ONLY when a candidate satisfies:
     - `ClassificationKind == MeshClassificationKind.KnownMesh`
     - `DetectedGameVersion == GameVersion.Sims3`
     - `RoleKind == MeshRoleKind.Geometry`
     - `TypeId == 0x015A1849` (TS3 GEOM magic TypeId)
     - `CanInspectCanonicalMesh == true`
   - **Object Model Package Alignment (`CONVG003`)**: If a source package contains object model resources (MODL `0x01661233` or MLOD `0x01D10F34`) but no direct TS3 GEOM (`0x015A1849`), `HasImportableMesh` MUST be `false` and controlled diagnostic issue **`CONVG003`** attached: `"No direct TS3 GEOM resource found; object model conversion requires MODL/MLOD decomposition."`
   - **Asset Retention**: Retains non-TS3 GEOM resources (MODL, MLOD, RIG, RSLT, COBJ, CBLK, BGEO, etc.) deterministically in `OtherResources` without dropping them.
   - **Graph Readiness**: `IsSourceGraphReady = packageInspection.IsSuccess && HasImportableMesh`.

3. **Conversion Service Integration**:
   - `DecorativeObjectConversionService` integrates `IDecorativeObjectSourceGraphBuilder` during conversion planning.
   - `STEP-06-SOURCE-GRAPH` (`Source Asset Graph Construction`) status MUST be `Failed` (or `Blocked`) if `IsSourceGraphReady` is `false`.
   - `DecorativeObjectConversionPlan.IsFeasible` MUST be `false` if `IsSourceGraphReady` is `false` (no importable TS3 GEOM).
   - Preserves `STEP-07-TS4-WRITER = NotImplemented`.

4. **Security & Boundary Guards**:
   - **Zero File Creation**: Graph construction is strictly read-only and analytical; no files are created or written to disk.
   - **Zero Low-Level Binary Parsing in Application Layer**: Enforced by `ApplicationBoundaryGuardTests`.

## Consequences
- Prevents false positive "convertible mesh present" reports on packages containing only MODL/MLOD model resources or textures without TS3 GEOM.
- Ensures non-mesh/texture resources (MODL, MLOD, RIG, RSLT, etc.) are retained deterministically in `OtherResources` for total package auditability.
- Maintains clean quality gates with 0 build warnings and passing unit tests.
