# DEC-0040: TS3 MODL/MLOD Object Model Decomposition Service & Source Graph Integration Architecture

- **Status**: Approved
- **Date**: 2026-09-04
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-CONV-004 / SIMS-CONV-004-R1

## Context
In real-world TS3 object packages (e.g., Onyx chopping board object package), direct TS3 GEOM (`0x015A1849`) resources are not present. Object geometry and references are defined in MODL (`0x01661233`), MLOD (`0x01D10F34`), RIG (`0x8EAF13DE`), and RSLT (`0xD3044521`) resources.
To advance the object conversion pipeline from a basic blocking status (`CONVG003`) to an inspectable, analyzed source graph with object model decomposition metadata, an Application-level decomposition service (`ITs3ObjectModelDecompositionService` / `Ts3ObjectModelDecompositionService`) and graph integration are required.

## Decision

1. **`ITs3ObjectModelDecompositionService` Contract & Implementation (`SimsConverter.Application`)**:
   - Contract: `ITs3ObjectModelDecompositionService` with `DecomposeAsync(string packageFilePath, CancellationToken)` and `Decompose(PackageInspectionResult)`.
   - Implementation: `Ts3ObjectModelDecompositionService` in `SimsConverter.Application.Services`.
   - Resource Collection: Filters target TS3 object model resources:
     - `MODL` (`0x01661233`)
     - `MLOD` (`0x01D10F34`)
     - `RIG` (`0x8EAF13DE`)
     - `RSLT` (`0xD3044521`)
   - Decoded Payload Extraction: Reads MODL/MLOD binary payloads strictly through `IPackageResourcePayloadReader` (guaranteeing decompressed byte spans/streams).
   - Metadata Parsing & Aggregation: Uses `ITs3ObjectModelMetadataReader` to parse decompressed payloads and aggregates results into immutable `Ts3ObjectModelDecompositionResult`.

2. **Decomposition Failure Semantics & Metadata Availability Guard (R1 Hardening)**:
   - **Aggregate Issue Tracking**: When payload extraction fails, `DECOMP002` error issues are explicitly appended to the aggregate `Issues` list (in addition to being recorded in individual model failure metadata results).
   - **Result Success Status**: `Ts3ObjectModelDecompositionResult.IsSuccess` evaluates to `false` whenever any Error-level issue (such as payload extraction failure or metadata parsing error) is present.
   - **Strict Metadata Availability Guard**: `HasDecompositionMetadata` requires both model resources present AND at least one successful metadata result: `(ModlCount > 0 || MlodCount > 0) && ModelMetadataResults.Any(m => m.IsSuccess)`.
   - **Graph Integration Guard**: `DecorativeObjectSourceGraphBuilder` appends `"(decomposition metadata available)"` to issue `CONVG003` strictly when `HasDecompositionMetadata` is `true`. Failure metadata results do NOT trigger metadata availability warnings.

3. **Source Graph Integration (`DecorativeObjectSourceGraphBuilder`)**:
   - `IsSourceGraphReady` strictly remains `false` when no direct TS3 GEOM mesh asset is available (initial conversion will not start automatically).
   - Carries the `ObjectModelDecomposition` result on `DecorativeObjectSourceAssetGraph`.

4. **Security & Governance Guards**:
   - **Zero Speculative Linking**: Automatic speculative links between mesh and texture assets remain strictly forbidden (`ResourceLinks` stays empty).
   - **Real Fixture Validation & Controlled Error Verification**: Real fixture tests cleanly skip if fixture files are missing, assert exact resource counts (1 MODL, 2 MLOD, 1 RIG, 1 RSLT) on target package fixtures, and verify controlled RefPack (`PKGP004`/`DECOMP002`) failure semantics.

## Consequences
- Unblocks decorative object model inspection at the Application layer for real-world TS3 packages with strict failure semantics.
- Preserves safety boundaries (no speculative linking, conversion start blocked until Phase 4 execution).
- Full DI container registration in `App.axaml.cs` and 100% pass rate on all quality gates across 298 tests.
