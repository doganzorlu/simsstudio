# DEC-0034: Decorative Object Conversion Boundary & Planning Skeleton Architecture

- **Status**: Approved
- **Date**: 2026-09-02
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-CONV-001

## Context
Phase 4 of SIMSStudio initiates the TS3 to TS4 Decorative Object Conversion workflow. To safely enter Phase 4 without prematurely attempting unvalidated file generation or introducing low-level binary parsing into the Application layer, a dedicated application boundary service (`IDecorativeObjectConversionService` / `DecorativeObjectConversionService`) is established in `SimsConverter.Application` to analyze source TS3 decorative object packages, collect mesh/texture conversion candidates, perform canonical import checks, and construct a deterministic conversion plan (`DecorativeObjectConversionPlan`).

## Decision

1. **Application Boundary Contract & Service (`SimsConverter.Application`)**:
   - Contract: `IDecorativeObjectConversionService` (`CreateConversionPlanAsync`, `CreateConversionPlan`).
   - Implementation: `DecorativeObjectConversionService` injecting `IPackageInspectionService`, `IMeshInspectionService`, and `ITextureInspectionService`.
   - Models: `DecorativeObjectConversionRequest`, `DecorativeObjectConversionResult`, `DecorativeObjectConversionPlan`, `DecorativeObjectConversionStep`, `DecorativeObjectConversionStepStatus`.

2. **Conversion Planning & Execution Flow**:
   - Step 1 (`STEP-01-VAL-SRC`): Validates source package path presence and existence (`CONVA001`).
   - Step 2 (`STEP-02-VAL-OUT`): Evaluates target output path canonical guard (`CONVA002`) and ensures the output path is not identical to the source path (`CONVA003`), preventing source package overwrite.
   - Step 3 (`STEP-03-INSPECT-SRC`): Orchestrates source package resource inspection via `IPackageInspectionService` (`CONVA004`).
   - Step 4 (`STEP-04-COLLECT-MESH`): Collects mesh candidates (`MeshClassificationKind.KnownMesh`) via `IMeshInspectionService`.
   - Step 5 (`STEP-05-COLLECT-TEX`): Collects texture candidates (`TextureClassificationKind.KnownTexture`) via `ITextureInspectionService`.
   - Step 6 (`STEP-06-GEOM-CHECK`): Verifies TS3 GEOM canonical mesh import availability (`CanInspectCanonicalMesh`).
   - Step 7 (`STEP-07-TS4-WRITER`): Evaluates TS4 target package writer capability, deterministically returning `NotImplemented` / `Planned` for this initial planning phase.

3. **Security & Boundary Enforcement**:
   - **Zero File Creation**: Conversion planning is strictly read-only and analytical; no target `.package` file or temporary output file is generated or written to disk.
   - **Overwrite Prevention Guard**: Output paths matching source package paths (accounting for path canonicalization via `Path.GetFullPath`) are rejected immediately with `CONVA003`.
   - **Zero Low-Level Binary Parsing in Application Layer**: No direct binary stream parsing, `File.ReadAllBytes`, or byte primitive slicing is allowed in `SimsConverter.Application`. Enforced by `ApplicationBoundaryGuardTests`.

## Consequences
- Establishes a clean, secure, and deterministic entry point for Phase 4 TS3 $\rightarrow$ TS4 decorative object conversion.
- Guarantees complete safety against unintended source file overwrites or corrupted target file creation.
- Preserves all architectural boundaries and quality gate standards across the codebase.
