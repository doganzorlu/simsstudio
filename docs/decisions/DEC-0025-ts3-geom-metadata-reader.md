# DEC-0025: TS3 GEOM Header & Structural Metadata Reader Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-004

## Context
Phase 3 of SIMSStudio requires establishing a safe, non-destructive, bounds-checked binary reader (`ITs3GeomMetadataReader` / `Ts3GeomMetadataReader`) in `SimsConverter.Mesh` aligned strictly with the official TS3 GEOM RCOL reference specification (`blender-sims3-geom` `geom_load.py` & `geom_write.py`) to parse Little-Endian binary header and structural layout metadata (`"GEOM"`, RCOL container version, GEOM version, vertex count, face/index count, vertex stride, bone count, TGI counts, shader ID) from Sims 3 GEOM (`0x015A1849`) raw payload buffers.

## Decision

1. **Deterministic Candidate Layout Validation & TGI Sequence**:
   - RCOL layout selection uses candidate evaluation (`EvaluateLayoutCandidate`) to prevent count values (e.g. `externalTgiCount = 1..10`) from being confused with numeric version headers:
     - **Candidate A (Count-First RCOL)**: `geom_write.py` default format starting directly with `externalTgiCount` (offset 0) and `internalResourceCount` (offset 4).
     - **Candidate B (Tagged RCOL)**: Container starting with `"RCOL"` signature tag.
     - **Candidate C (Numeric Version RCOL)**: Container starting with `rcolVersion` integer (1..10).
   - Candidate is valid only if internal ITG has `TypeId == 0x015A1849`, location table entry exists, and `"GEOM"` magic signature is verified at chunk offset.
   - **Internal ITG Array FIRST**: `InternalResourceCount` 16-byte entries (`InstanceId` uint64 LE at offset 0..7, `TypeId` uint32 LE at offset 8..11, `GroupId` uint32 LE at offset 12..15).
   - **External Resource Array SECOND**: `ExternalTgiCount` 16-byte entries.
   - **Chunk Location Table THIRD**: `InternalResourceCount` 8-byte entries (`Position` uint32 LE, `Size` uint32 LE).
   - **Strict Chunk Slice Boundary Guard (`GEOM004`)**: Verifies `ChunkOffset + Size <= buffer.Length`.

2. **Writer-Exact GEOM Chunk Layout (`geom_write.py` lines 62, 70, 100-120, 143)**:
   - Reads GEOM chunk magic (`"GEOM"`), version, `rawTgiOffset`, `tgiSize`, `EmbeddedId`.
   - **Exact `tgiOffset` Formula (`geom_write.py` lines 62 & 143)**: `actualTgiPos = 12 + rawTgiOffset` (where 12 is `tgiOffsetFieldOffset 8 + 4`).
   - **Embedded TGI Tail Size Validation (`GEOM005`)**: Verifies `actualTgiPos + tgiSize <= chunkSpan.Length` and checks exact size formula `tgiSize == 4 + tgiCount * 16`. Legacy payloads without TGI tail (`tgiSize == 0`) are supported explicitly.
   - **MTNF Block & Zero Padding Support (`geom_write.py` line 70)**: Detects optional `"MTNF"` shader block (`MtnfSize + 8` bytes) using `checked` overflow arithmetic (`GEOM004`/`GEOM005`), or skips 4-byte zero padding word when `EmbeddedId == 0` and MTNF is omitted.
   - Reads `MergeGroup` (int32), `SortOrder` (int32), `VertexCount` (uint32), `VertexElementCount` (uint32).
   - Parses **9-byte vertex element descriptors** (`DataType` uint32 LE at offset 0..3, `Format` uint32 LE at offset 4..7, `SizeBytes` byte at offset 8) and calculates `VertexStrideBytes` directly by summing `SizeBytes`.
   - Skips Vertex Buffer (`VertexCount * VertexStrideBytes`).
   - Parses Face Group Section strictly following `geom_write.py` lines 100-120: Reads `groupMarker` (`uint32` LE, 4B), `faceFormat` (`byte`, 1B), `facePointCount` (`uint32` LE, 4B), verifies `facePointCount % 3 == 0` (`GEOM003`), and skips Index Buffer (`facePointCount * 2` bytes). Old fake "faceGroupCount + 5B header" layout is explicitly rejected.
   - Reads `SkinControllerIndex` (int32) and `BoneCount` (uint32).
   - **Strict Bone Hash Truncation Guard (`GEOM005`)**: Verifies `curr + boneCount * 4 <= chunkSpan.Length`.

3. **Dedicated Reader Contract & Models (`SimsConverter.Mesh`)**:
   - `Ts3GeomParseResult Read(ReadOnlySpan<byte> buffer)`
   - `Ts3GeomParseResult Read(Stream stream)`
   - `Task<Ts3GeomParseResult> ReadAsync(Stream stream, CancellationToken cancellationToken = default)`
   - `Ts3GeomMetadata` record: `ContainerFormat`, `RcolVersion`, `GeomVersion`, `VertexCount`, `FaceCount`, `IndexCount`, `VertexStrideBytes`, `BoneCount`, `ExternalTgiCount`, `InternalTgiCount`, `ShaderId`, `TotalSizeBytes`.
   - `Ts3GeomParseResult` record: `IsSuccess`, `Ts3GeomMetadata?`, `Issues`.

4. **Validation & Error Codes**:
   - **`GEOM000`**: Buffer/stream is null, unreadable, or shorter than minimum header size.
   - **`GEOM001`**: Magic signature is not `"GEOM"` or `"RCOL"`.
   - **`GEOM002`**: Unsupported GEOM format version outside range $1 \le \text{Version} \le 12$.
   - **`GEOM003`**: Invalid structural count or stride (`VertexCount = 0`, `IndexCount = 0`, `VertexStrideBytes = 0`, or `FacePointCount % 3 != 0`).
   - **`GEOM004`**: Arithmetic overflow or out-of-bounds section offset when calculating 16-byte TGI, 8-byte Location Table, chunk slice boundaries, or MTNF block size (`Position + Size > buffer.Length`).
   - **`GEOM005`**: Truncated payload buffer shorter than required by section headers (including embedded TGI formula mismatch `tgiSize != 4 + count * 16`, MTNF size truncation, or bone hash tail).

5. **Zero 3D Mesh Decoding or Intermediate Representation**:
   - Performs zero vertex attribute unpacking, index buffer decoding, `CanonicalMesh` model instantiation, 3D preview rendering, or file format translation.

6. **Stream Invariants**:
   - Reading from seekable streams preserves stream position invariants (`stream.Position` restored on error).

## Consequences
- Enables accurate, safe inspection of real TS3 RCOL-wrapped GEOM payloads aligned with reference specifications (`blender-sims3-geom`).
- Rejects legacy unaligned fake headers.
- Guarantees complete bounds checking and candidate layout validation.
- Preserves existing decision records (`DEC-0024` status Approved).
- Maintains 100% test coverage and quality gate compliance across 6 solution projects.
