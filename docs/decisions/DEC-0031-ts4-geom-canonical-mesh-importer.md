# DEC-0031: TS4 CAS GEOM Vertex & Index Decoder to CanonicalMesh Importer Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-010-R2

## Context
Phase 3 of SIMSStudio requires establishing a safe vertex and index buffer importer (`ITs4GeomCanonicalMeshImporter` / `Ts4GeomCanonicalMeshImporter`) to decode *The Sims 4* (TS4) CAS GEOM (`0x015A1849` / `TsSharedGeom`) binary payloads into domain `CanonicalMesh` entities.

## Decision

1. **Contract & Model Definition (`SimsConverter.Mesh`)**:
   - Interface: `ITs4GeomCanonicalMeshImporter` (`Import(byte[] buffer, string? meshName = null)`, `ImportAsync(...)`).
   - Implementation: `Ts4GeomCanonicalMeshImporter` in `SimsConverter.Mesh.Services`.
   - Result Model: `Ts4GeomImportResult` (`bool IsSuccess`, `CanonicalMesh? Mesh`, `IReadOnlyList<ConversionIssue> Issues`).

2. **Vertex & Index Decoding Pipeline**:
   - Consumes verified structural metadata from `ITs4GeomMetadataReader`.
   - Reads 9-byte vertex element descriptors (`datatype` 4B, `format` 4B, `sizeBytes` 1B).
   - Supports Datatypes: Position (`1`), Normal (`2`), UV0 (`3`), Bone Indices (`4`), Bone Weights (`5`), Tangent (`6`), Vertex ID (`10`).
   - **Bone Weight Format & Size Guard Policy (`datatype == 5`)**:
     - `format == 1 && sizeBytes >= 16`: Decodes Float4 bone weights (`4 x float32` = 16 bytes per vertex).
     - `format == 2 && sizeBytes >= 4`: Decodes Byte4 normalized bone weights (`4 x byte / 255.0f` = 4 bytes per vertex).
     - Unsupported format or insufficient `sizeBytes` (e.g. `format == 99`, `format == 1` with `sizeBytes < 16`, or `format == 2` with `sizeBytes < 4`): Returns controlled error `MESHG008` (`"Unsupported or invalid bone weight format (Format={format}, Size={sizeBytes}B)."`).
   - Unrecognized Datatype: Emits diagnostic warning (`MESHG003`) and advances cursor safely by `sizeBytes`.
   - Missing Position Descriptor (`datatype == 1`): Returns controlled error (`MESHG004`).
   - Index Buffer Decoding: Supports `bytesPerFacePoint` values `1` (UInt8), `2` (UInt16), `4` (UInt32). Value `0` defaults to `2` (UInt16). Unsupported values (e.g. `5`) return controlled error (`MESHG007`).

3. **CanonicalMesh Production Policy**:
   - `SourceGameVersion = GameVersion.Sims4`.
   - `CoordinateSystem = CanonicalCoordinateSystem.RightHandedYUp` (Standard right-handed 3D coordinate system used across TS3/TS4 3D geometry importers).
   - Validates resulting `CanonicalMesh` using `ICanonicalMeshValidator.Validate(mesh)` prior to returning `Ts4GeomImportResult`.

4. **Strict Scope & Immutability Rules**:
   - **Zero** binary parsing in Application layer.
   - **Zero** writer/export or TS4 `.package` creation logic.
   - **Zero** MODL/MLOD parsing.
   - **Zero** UI changes.
   - Source payload buffer is **never** mutated during import (verified by unit test `Import_DoesNotMutateSourceBuffer`).

5. **Fault Tolerance & Diagnostic Issue Codes**:
   - `MESHG000`: Buffer null.
   - `MESHG001`: Chunk offset/size exceeds buffer boundaries.
   - `MESHG002`: Truncated vertex or index buffer payload.
   - `MESHG003`: Unrecognized vertex element descriptor datatype (Warning).
   - `MESHG004`: Missing Position vertex element descriptor (Error).
   - `MESHG007`: Unsupported `bytesPerFacePoint` index format value (Error).
   - `MESHG008`: Unsupported or invalid bone weight descriptor format/size combination (Error).

## Consequences
- Establishes a verified, crash-resilient vertex and index decoder supporting both Float4 and Byte4 bone weight representations with strict descriptor validation (`MESHG008`) for TS4 CAS GEOM payloads.
- Preserves existing decision records (`DEC-0024`, `DEC-0025`, `DEC-0026`, `DEC-0027`, `DEC-0028`, `DEC-0029`, `DEC-0030` status Approved).
- Maintains quality gate compliance across solution projects.
