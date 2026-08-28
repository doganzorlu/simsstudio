# DEC-0004: Package Inspection Application Boundary Architecture

- **Status**: Decision Drafted / Pending Approval
- **Date**: 2026-08-27
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-PKG-003

## Context
SIMSStudio requires a clean application use-case boundary (`IPackageInspectionService` / `PackageInspectionService`) to mediate between the binary package parser (`IDbpfPackageParser`) and future presentation surfaces (e.g. Avalonia UI resource inspector windows/views).

## Decision
We establish the application boundary in `SimsConverter.Application` following these rules:

1. **Clean Layer Isolation**:
   - `SimsConverter.Application` consumes `IDbpfPackageParser` from `SimsConverter.Package` via contract interfaces.
   - Application layer performs ZERO binary byte offset parsing.
   - Application layer contains ZERO references to Avalonia UI or presentation assemblies (`SimsConverter.App`).

2. **Model Mapping Strategy**:
   - Maps raw `DbpfParseResult` entries into presentation-ready `PackageResourceRow` value records.
   - Exposes formatted hexadecimal representations (`TypeHex`, `GroupHex`, `InstanceHex`, `FormattedKey`) and human-readable compression names (`CompressionName`).
   - Preserves diagnostic `ConversionIssue` records from lower layers without swallowing or masking warnings/errors.

3. **Safe Asynchronous Execution**:
   - `InspectFileAsync` accepts `CancellationToken` and operates non-destructively on package files.
   - Malformed files or parser failures return structured `PackageInspectionResult.Failure` containing diagnostic issues without throwing uncaught exceptions.

## Consequences
- UI components in `SimsConverter.App` can bind directly to `PackageResourceRow` view models without binary knowledge.
- Application logic can be tested in isolation (`SimsConverter.Application.Tests`) without GUI rendering.
- Preserves clean architecture boundaries across the solution.
