# DEC-0033: Phase 3 Mesh Real Fixture Validation & Compatibility Matrix Architecture

- **Status**: Pending Approval
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-012-R2

## Context
Phase 3 of SIMSStudio requires establishing real-world fixture validation and a compatibility matrix (`docs/reports/mesh_compatibility_report.md`) to officially finalize the Phase 3 Mesh Pipeline without committing proprietary binary `.package` files to the git repository.

## Decision

1. **Local Fixture Directory & Version Control Policy**:
   - Real binary sample `.package` files MUST be stored under `fixtures/local/mesh/` or `fixtures/local/`.
   - `.gitignore` MUST enforce exclusions for `fixtures/local/` to prevent committing proprietary game or custom content binary fixtures to git.

2. **Automated Test Harness Policy (`MeshInspectionRealFixtureValidationTests`)**:
   - Test harness in `tests/SimsConverter.Application.Tests` scans `fixtures/local/mesh/` exclusively for sample `*.package` files.
   - If zero `*.package` fixture files are present in `fixtures/local/mesh/`, the test MUST complete cleanly (`[Fact]` with assertion success message) without breaking CI/CD build runs.
   - Raw `.geom` files and `.sims3pack` containers are tested via dedicated unit test suites (`Ts3GeomMetadataReaderTests`, `Ts4GeomMetadataReaderTests`, `Sims3PackInspectionServiceTests`).
   - When `*.package` fixture files are present, the test executes end-to-end package inspection, mesh resource classification, payload reading, TS3/TS4 GEOM canonical mesh importing, and summary field population verification.

3. **Phase 3 Mesh Pipeline Finalization Scope**:
   - **TS3 CAS GEOM Importer**: Fully verified across Count-First, Tagged `"RCOL"`, and Numeric Version RCOL layouts.
   - **TS4 CAS GEOM Importer**: Fully verified across GEOM versions $5, 12, 13, 14$, MTNF shader block skipping, variable UV stitch loops, Float4 (`format == 1`), and Byte4 normalized (`format == 2`) bone weights.
   - **Application Boundary Integration**: `MeshInspectionService` dispatches TS3 and TS4 GEOM resources deterministically to `ITs3GeomCanonicalMeshImporter` and `ITs4GeomCanonicalMeshImporter`.

4. **Strict Scope Restrictions**:
   - **Zero** binary fixture files committed to git.
   - **Zero** writer/export or conversion operations in Phase 3.
   - **Zero** TS4 `.package` creation logic.
   - **Zero** UI changes.

## Consequences
- Official completion of Phase 3 Mesh Pipeline.
- Establishes a clean transition boundary for Phase 4 (TS3 $\rightarrow$ TS4 decorative object conversion skeleton).
- Preserves existing decision records (`DEC-0024`, `DEC-0025`, `DEC-0026`, `DEC-0027`, `DEC-0028`, `DEC-0029`, `DEC-0030`, `DEC-0031`, `DEC-0032` status Approved).
- Maintains quality gate compliance across solution projects.
