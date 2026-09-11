# DEC-0045: Real TS4 Fixture Provenance & Non-Synthetic Validation Boundary

- **Status**: Approved
- **Date**: 2026-09-08
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-CONV-015-R1

## Context
During reverse conversion validation (`SIMS-CONV-015`), a programmatically generated minimal byte buffer was used as a synthetic fallback when local real-world fixture files were absent. Quality gate reviews identified that synthetic fixtures (~616 bytes) do not reflect real-world game package complexity (multi-LOD geometry, full material graphs, complex RCOL chunk tables, COBJ/OBJD catalogs). As a result, CI could report success without actually validating a real TS4 package.

## Decision

1. **Strict Separation of Synthetic Unit Tests vs Real Fixture Integration Validation**:
   - **Synthetic Unit Tests**: Use lightweight mock objects or isolated byte streams strictly to test individual unit algorithms (parsers, classifiers, bitpackers).
   - **Real Fixture Integration Tests**: MUST execute exclusively against non-synthetic real-world fixtures derived from real game packages or converted real-world Sims3Pack assets (`fixtures/local/`).
   - **No Synthetic Fallback**: Real fixture validation test harnesses MUST NOT auto-generate synthetic byte streams as fallbacks when fixture files are missing.

2. **Graceful Skipped State Policy for Real Fixtures**:
   - If local real-world fixture files are missing in `fixtures/local/`, the test harness MUST output a controlled `[SKIPPED]` log message and pass gracefully without generating dummy files or throwing unhandled exceptions.

3. **Non-Synthetic Fixture Provenance & Metric Enforcement**:
   - When a real fixture file is present, the test harness enforces empirical provenance criteria to guarantee non-synthetic authenticity:
     - **File Size Constraint**: Package size must exceed 5,000 bytes (`Length > 5000`).
     - **Resource Entry Density**: Package index must contain multiple resource entries (`ResourceCount > 3`).
     - **Resource Type Diversity**: Package index must contain diverse resource types across object definition, model header, mesh LOD, and material graphs (`TypeId distinct count >= 3`).

4. **UI End-to-End Reverse Conversion Flow**:
   - The test harness validates the full UI reverse conversion pipeline (`ResourceInspectorViewModel`):
     1. Open real TS4 package.
     2. Verify automatic `TS4 -> TS3` direction recommendation (`TargetGameVersion = GameVersion.Sims3`).
     3. Execute `ConvertCommand` to produce TS3 package file.
     4. Validate output TS3 package with DBPF package parser (`0x01661233` MODL, `0x01D10F34` MLOD, `0x015A1849` GEOM).
     5. Execute `InspectConvertedPackageCommand` to inspect converted output via UI.

## Consequences
- Prevents false-positive test passes in CI by removing synthetic file generators from real fixture test suites.
- Ensures robust validation against complex real-world game assets while maintaining zero binary clutter in version control.
- Guarantees complete pipeline coverage for reverse conversion (`TS4 -> TS3`).
