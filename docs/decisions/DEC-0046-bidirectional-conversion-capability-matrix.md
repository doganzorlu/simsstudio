# DEC-0046: Bidirectional Conversion Capability Matrix

- **Status**: Approved
- **Date**: 2026-09-09
- **Deciders**: SimsConverter Core Architecture Team
- **Task Reference**: SIMS-CONV-016 — Bidirectional Conversion Capability Matrix

## Context & Problem Statement

SimsConverter supports bidirectional conversion of decorative objects between **The Sims 3** (TS3) and **The Sims 4** (TS4). However, DBPF package files from both games contain a wide variety of resource types. While object model, catalog definition, LOD meshes, materials, and textures are fully convertable or mapped bidirectionally, game-specific tuning (e.g. TS3 `ITUN` / `BCON`) or script binaries (e.g. TS4 `S4SCRIPT`) cannot be mapped into the opposing game's runtime engine.

To ensure transparent operational safety and UI feedback, SimsConverter requires a structured capability matrix evaluation boundary that explicitly reports resource-level support status (`Supported`, `PassThrough`, `Unsupported`) and emits controlled UI warning issues (`CAPA001`) without breaking container generation or failing conversion feasible paths.

## Decision Drivers

1. **Explicit Resource-Level Capability Classification**: Every DBPF resource type must be categorized as `Supported` (transformed), `PassThrough` (preserved neutral metadata), or `Unsupported` (game-specific, skipped).
2. **Non-Blocking Controlled Warnings**: Unsupported resource types must trigger user-visible `CAPA001` warning issues in the UI (`ResourceInspectorViewModel`) without aborting valid object conversions.
3. **Bidirectional Enforcement**: Must evaluate capability matrices for both `TS3 -> TS4` and `TS4 -> TS3` conversion directions.
4. **Staging & Rollback Preservation**: Conversions must maintain atomic file write staging and rollback protection across both directions.

## Technical Architecture & Matrix Mapping

### Resource Capability Classification Table

| DBPF Resource Type | TypeId Hex | Name / Purpose | TS3 -> TS4 Capability | TS4 -> TS3 Capability | Action Taken |
|-------------------|------------|----------------|----------------------|----------------------|--------------|
| **COBJ** | `0x319E4F1D` | Catalog Object | `Supported` | `Supported` | Identity / Canonical Catalog Mapping |
| **OBJD** | `0xC0DB5AE7`, `0x02DC343F` | Object Definition | `Supported` | `Supported` | Metadata & Identity Transformation |
| **MODL** | `0x01661233` | Object Model (LOD Index) | `Supported` | `Supported` | RCOL / Object Model Index Mapping |
| **MLOD** | `0x01D10F34` | Model LOD Mesh Group | `Supported` | `Supported` | Canonical Mesh LOD Construction |
| **GEOM** | `0x015A1849`, `0x015A182C` | Geometry Mesh | `Supported` | `Supported` | Canonical Mesh Import/Export |
| **RMAT** | `0x2172D019`, `0x01D0E75D` | Material Definition | `Supported` | `Supported` | Material Shader & Texture Link Assembly |
| **RIG** | `0x8EAF13DE` | Skeleton Rig | `Supported` | `Supported` | Identity Skeleton Payload Pass-Through |
| **RSLT** | `0xD3044521`, `0x05B47D14` | Slot Layout | `Supported` | `Supported` | Identity Slot Layout Pass-Through |
| **_IMG / DDS** | `0x00B2D882`, `0xDB43D069` | Texture Images | `Supported` | `Supported` | Raw DDS Inspection & Payload Verification |
| **STBL** | `0x220557DA`, `0x220557DB` | String Tables | `PassThrough` | `PassThrough` | Preserved Neutral Container Metadata |
| **THUM / ICON** | `0x0D64DFF0`, `0x0D64DFF1` | Thumbnails & Icons | `PassThrough` | `PassThrough` | Preserved Neutral Container Metadata |
| **ITUN** | `0x03B33DDF` | Interaction Tuning | `Unsupported` | `Unsupported` | Skipped + `CAPA001` Warning Emitted |
| **BCON** | `0x03B33DDE` | Binary Constants | `Unsupported` | `Unsupported` | Skipped + `CAPA001` Warning Emitted |
| **VPXY** | `0x73878036` | Visual Proxy | `Unsupported` | `Unsupported` | Skipped + `CAPA001` Warning Emitted |
| **S4SCRIPT** | `0x2800D61B` | Python Script | `Unsupported` | `Unsupported` | Skipped + `CAPA001` Warning Emitted |
| **CASP** | `0x034AEECB` | CAS Part | `Unsupported` | `Unsupported` | Skipped + `CAPA001` Warning Emitted |

| **Unknown / Unrecognized** | `0x...` | Any Unlisted TypeId | `Unsupported` | `Unsupported` | Skipped + `CAPA002` Warning Emitted |

## Implementation Details

- **`ConversionCapabilityStatus` Enum**: `Supported` (`0`), `PassThrough` (`1`), `Unsupported` (`2`).
- **Strict Whitelist PassThrough Policy**: Only explicitly whitelisted neutral metadata types (`STBL`, `THUM`, `ICON`) are classified as `PassThrough`. Unrecognized TypeIds are never passed through implicitly.
- **Controlled Warning Issues**:
  - `CAPA001`: Emitted for known game-specific unsupported resources (`ITUN`, `BCON`, `VPXY`, `S4SCRIPT`, `CASP`, `COMP`).
  - `CAPA002`: Emitted for unknown/unrecognized resource TypeIds not listed in the conversion whitelist.
- **`IDecorativeObjectConversionCapabilityService` / `DecorativeObjectConversionCapabilityService`**: Evaluates `PackageInspectionResult` and generates `DecorativeObjectConversionCapabilityMatrix`.
- **`DecorativeObjectConversionService` Integration**: Includes `STEP-03B-CAPABILITY-MATRIX` step during conversion plan creation and attaches the capability matrix to `DecorativeObjectConversionPlan`.
- **UI ViewModel (`ResourceInspectorViewModel`)**: Exposes capability issues (`CAPA001` / `CAPA002`) directly in the UI issue list and conversion summary.

## Consequences

- **Positives**: Transparent, resource-by-resource conversion insight; strict isolation preventing unverified binary payloads from being passed through into target package outputs.
- **Verification**: Complete unit test coverage (`DecorativeObjectConversionCapabilityServiceTests`), 0 build warnings/errors, and full test suite execution.
