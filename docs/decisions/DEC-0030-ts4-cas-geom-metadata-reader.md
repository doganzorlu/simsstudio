# DEC-0030: TS4 CAS GEOM Metadata Reader Foundation Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-009-R4

## Context
Phase 3 of SIMSStudio requires establishing a safe structural metadata reader foundation (`ITs4GeomMetadataReader` / `Ts4GeomMetadataReader`) for *The Sims 4* (TS4) CAS GEOM (`0x015A1849` / `TsSharedGeom`) payloads prior to vertex buffer decoding or canonical mesh importing.

## Decision

1. **Contract & Model Definition (`SimsConverter.Mesh`)**:
   - Interface: `ITs4GeomMetadataReader` (`ReadMetadata(ReadOnlySpan<byte> buffer, string? resourceKey = null)`).
   - Implementation: `Ts4GeomMetadataReader` in `SimsConverter.Mesh.Services`.
   - Result Model: `Ts4GeomParseResult` (`bool IsSuccess`, `Ts4GeomMetadata? Metadata`, `IReadOnlyList<ConversionIssue> Issues`).
   - Metadata Record: `Ts4GeomMetadata` (`ContainerFormat`, `GeomVersion`, `ShaderHash`, `MtnfSizeBytes`, `MergeGroup`, `SortOrder`, `VertexCount`, `FaceCount`, `FacePointCount`, `VertexStrideBytes`, `VertexElementCount`, `NumSubMeshes`, `UvStitchCount`, `SeamStitchCount`, `SlotrayCount`, `BoneCount`, `EmbeddedTgiCount`, `TotalSizeBytes`, `GeomChunkOffset`, `GeomChunkSize`, `TgiOffset`, `TgiSize`).

2. **Reader Responsibilities & TS4 Byte-Exact Layout Alignment**:
   - Supports raw GEOM buffers starting with `"GEOM"` magic at offset 0 and RCOL-wrapped TS4 GEOM container buffers (`TryFindRcolGeomChunk`).
   - Aligns RCOL header reading with `TS4-SimRipper` `GEOM.ReadFile`: `version1` (4B), `count` (4B), `ind3` (4B), `extCount` (4B), `intCount` (4B), `dummyTGI` (16B), `abspos` (4B), `meshsize` (4B).
   - GEOM Chunk Header: magic `"GEOM"` (4B), `geomVersion` (4B), `rawTgiOffset` (4B), `tgiSize` (4B), `shaderHash` (4B).
   - MTNF Shader Block: If `shaderHash != 0`, reads `mtnfSizeBytes` (`uint32`) and skips `mtnfSizeBytes` bytes.
   - Reads `mergeGroup` (4B), `sortOrder` (4B), `vertexCount` (4B), `elementCount` (4B), vertex element descriptors (`elementCount * 9B`), and skips vertex float buffers (`vertexCount * stride`).
   - Reads submesh face section: `numSubMeshes` (`uint32`), `bytesPerFacePoint` (1B), `numFacePoints` (`uint32`), face index buffer.
   - Version Stitches & Slotrays:
     - `geomVersion == 5`: skips `skconIndex` (`int32`).
     - `geomVersion >= 12`: Reads `uvStitchCount` (4B). Loops `uvStitchCount` times, reading `vertexIndex` (4B) and `coordCount` (4B) per entry, skipping `coordCount * 8` bytes per entry.
     - `geomVersion >= 13`: `seamStitchCount` (4B) + (if `seamStitchCount > 0`: `seamStitchCount * 6` bytes).
     - `slotrayCount` (4B) + (if `slotrayCount > 0`: `slotrayCount * (geomVersion < 14 ? 63 : 66)` bytes).
   - Bone Section: `boneHashCount` (`int32`) + bone hashes (`boneHashCount * 4` bytes).
   - Tail TGI List: `numtgi` (`int32`) at `rawTgiOffset` (`TGIoff`).
   - Validates TS4 GEOM version range: Version $5$ (legacy TS3/shared compatibility), Base Game $12$, and expansion versions $13, 14$ (`GEOM002`).

3. **Strict Scope & Immutability Rules**:
   - **Zero** vertex or index buffer decoding is performed in this task.
   - **Zero** `CanonicalMesh` generation is performed in this task.
   - **Zero** TS4 MODL/MLOD parsing or writing is performed in this task.
   - Source buffer is **never** mutated during metadata reading (verified by unit test `ReadMetadata_DoesNotMutateSourceBuffer`).

4. **Fault Tolerance & Diagnostic Issue Codes**:
   - `GEOM000`: Buffer null or shorter than 20-byte minimum header guard.
   - `GEOM001`: Magic signature is not `"GEOM"` or valid TS4 RCOL container.
   - `GEOM002`: Unsupported GEOM version outside supported TS4 set ($5, 12..14$).
   - `GEOM003`: Invalid structural count (`VertexCount == 0`, `VertexStrideBytes == 0`, `FacePointCount % 3 != 0`, or negative count).
   - `GEOM004`: Arithmetic overflow detected while calculating section offsets or buffer lengths.
   - `GEOM005`: Payload buffer truncated before expected section boundary.

## Consequences
- Establishes a byte-exact metadata reader aligned 1-to-1 with `TS4-SimRipper` `GEOM.cs` reference specifications.
- Preserves existing decision records (`DEC-0024`, `DEC-0025`, `DEC-0026`, `DEC-0027`, `DEC-0028`, `DEC-0029` status Approved).
- Maintains quality gate compliance across solution projects.
