# DEC-0043: TS4 Decorative Object Package Writer Boundary

- **Status:** Approved
- **Date:** 2026-09-07
- **Author:** SIMSStudio Core Team

---

## Context and Problem Statement

Following the construction of the immutable `DecorativeObjectConversionInputBundle` (SIMS-CONV-006 / SIMS-CONV-006-R2), the pipeline requires a robust, atomic target package writer boundary to transform conversion input bundles into fully formed TS4 DBPF 2.0 package containers.

Without a strict writer boundary:
1. Target package writes might accidentally overwrite the source `.package` file.
2. Interrupted or failing writes might leave corrupted or zero-byte `.package` files on disk.
3. Payload write order or index table alignment could violate the DBPF 2.0 specification.

---

## Proposed Solution

Introduce a formal TS4 package writing boundary consisting of:
1. `DecorativeObjectPackageWriteResourceEntry`: Represents an immutable planned resource entry with explicit `ResourceId`, `FormattedKey`, `Payload` bytes, and compression parameters.
2. `DecorativeObjectPackageWritePlan`: Represents a deterministic write execution plan generated from `DecorativeObjectConversionInputBundle`.
3. `IDecorativeObjectPackageWritePlanBuilder` / `DecorativeObjectPackageWritePlanBuilder`: Builds the write plan, collecting payloads from mesh bundles, texture candidates, and other resources, and sorting planned resources deterministically by `FormattedKey` (`StringComparison.Ordinal`).
4. `IDecorativeObjectPackageWriter` / `DecorativeObjectPackageWriter`: Executes the write plan cleanly using:
   - **Path Overwrite Guard (`WRIT001`)**: Prohibits `TargetOutputPath` matching `SourcePackagePath`.
   - **Atomic Temp-File + Move Strategy**: Writes to a temporary `.tmp.<guid>` file first and executes `File.Move(..., overwrite: true)` upon complete success. If an exception occurs, the temporary file is immediately deleted.
   - **DBPF 2.0 Compliance**: Emits standard 96-byte DBPF 2.0 headers and 32-byte index entries.

---

## Diagnostic Codes

| Code | Severity | Description |
| :--- | :--- | :--- |
| `WRIT000` | Error | Conversion input bundle or write plan is null or invalid. |
| `WRIT001` | Error | Target output path matches source package path; overwriting source is prohibited. |
| `WRIT002` | Error | Package file write or atomic filesystem move failed. |

---

## Consequences

- Zero risk of overwriting or corrupting source `.package` files.
- Zero leftover partial or corrupted output files on failure.
- Full compatibility with `DbpfPackageParser` (DBPF 2.0 header and index structure).
