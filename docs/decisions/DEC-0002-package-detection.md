# DEC-0002: Safe Package Detection Foundation

- **Status**: Approved
- **Date**: 2026-08-27
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-PKG-001

## Context
SIMSStudio needs to inspect incoming package files safely to determine whether they are DBPF package containers. Because DBPF package magic headers ("DBPF") are shared across multiple game generations (e.g. Sims 3 and Sims 4), identifying magic bytes alone is insufficient to identify the specific game version.

## Decision
We implement a safe, bounds-checked container detection model (`IPackageDetector` / `PackageDetector`) following these rules:

1. **Safe Bounds Checking**:
   - Short buffers (< 4 bytes), invalid magic, or truncated headers return controlled `PackageDetectionResult` containing diagnostic warnings/issues.
   - Operations never throw uncaught exceptions or crash the application on malformed input.

2. **Strict Prohibition of Magic-Only Guessing**:
   - Detecting "DBPF" magic sets `ContainerKind` to `PackageContainerKind.Dbpf` and `Confidence` to `PackageDetectionConfidence.Low`.
   - `DetectedGameVersion` MUST remain `GameVersion.Unknown` until explicit game-specific resource type signatures are verified in subsequent features.

3. **Read-Only / Non-Destructive Operation**:
   - Stream and file inspections read data read-only with shared access (`FileShare.Read`).
   - Source files are never modified or overwritten.

## Consequences
- Prevents false-positive game version classification during initial file inspection.
- Provides a robust foundation for UI and Application layer file inspection without binary parsing leakage.
- Ensures total stability against invalid or corrupt binary files.
