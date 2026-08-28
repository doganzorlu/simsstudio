# DEC-0015: Sims3Pack Real Fixture Validation Harness & Compatibility Matrix

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-S3P-007

## Context
SIMSStudio requires an automated test harness to validate end-to-end Sims3Pack inspection, XML metadata parsing, archive payload catalog scanning, raw DBPF package export, and DBPF header/index parsing against real-world `.sims3pack` binary files without committing proprietary game assets or user content to the git repository.

## Decision

1. **Local Fixture Policy & `.gitignore` Exclusions**:
   - Real-world `.sims3pack` files MUST be placed in `fixtures/local/` (relative to repository root).
   - `.gitignore` explicitly ignores `fixtures/local/` and `artifacts/exports/`. No binary `.sims3pack` files are committed to version control.

2. **Graceful Skipped State Validation Harness**:
   - `Sims3PackRealFixtureValidationTests.cs` scans `fixtures/local/` for `*.sims3pack` files.
   - If `fixtures/local/` does not exist or contains 0 `.sims3pack` files, the test harness logs `[SKIPPED] No local real-world .sims3pack fixtures found in fixtures/local/. Synthetic unit tests passed.` and passes cleanly without failing CI/CD quality gates.

3. **End-to-End Real-World Pipeline Validation**:
   - When real `.sims3pack` files are present in `fixtures/local/`, the test harness executes:
     1. Container detection (`ISims3PackDetector`)
     2. XML manifest parsing (`ISims3PackXmlParser`)
     3. Archive catalog scanning (`ISims3PackPayloadCatalogScanner`)
     4. Raw binary DBPF payload export (`ISims3PackPayloadExporter`)
     5. Exported `.package` header & index validation (`IDbpfPackageParser`)
   - Rejects corrupted or invalid payloads with controlled diagnostic issue codes.

4. **Compatibility Matrix Documentation**:
   - Outputs a Markdown compatibility report to [docs/reports/sims3pack_compatibility_report.md](../reports/sims3pack_compatibility_report.md) detailing detection status, XML parse status, catalog entries count, DBPF candidate count, raw export status, exported package inspection status, and issue codes.

## Consequences
- Guarantees end-to-end validation of real-world `.sims3pack` assets prior to building conversion engines.
- Protects the git repository from binary bloat and copyright risks.
- Maintains 100% build and test suite stability across all environments.
