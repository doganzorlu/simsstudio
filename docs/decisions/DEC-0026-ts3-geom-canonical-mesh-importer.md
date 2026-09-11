# DEC-0026: TS3 GEOM Vertex & Index Decoder to CanonicalMesh Importer Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-005

## Context
Phase 3 of SIMSStudio requires establishing a safe, non-destructive, bounds-checked binary mesh importer (`ITs3GeomCanonicalMeshImporter` / `Ts3GeomCanonicalMeshImporter`) in `SimsConverter.Mesh` aligned strictly with the official TS3 GEOM reference specification (`blender-sims3-geom` `geom_load.py` & `geom_write.py`) to decode binary vertex element buffers and index buffers into game-agnostic `CanonicalMesh`, `CanonicalVertex`, `CanonicalFace`, and `CanonicalBoneWeight` domain models, followed by validation using `ICanonicalMeshValidator`.

## Decision

1. **Dedicated Importer Service & Contract (`SimsConverter.Mesh`)**:
   - Contract: `ITs3GeomCanonicalMeshImporter` (`Import`, `ImportAsync`).
   - Implementation: `Ts3GeomCanonicalMeshImporter` injecting `ITs3GeomMetadataReader` and `ICanonicalMeshValidator`.
   - Returns immutable `Ts3GeomImportResult` (`IsSuccess`, `CanonicalMesh?`, `IReadOnlyList<ConversionIssue> Issues`).

2. **Strict Chunk Boundary Alignment via Metadata Reader**:
   - Importer consumes `GeomChunkOffset` and `GeomChunkSize` strictly from `Ts3GeomMetadata` produced by `Ts3GeomMetadataReader`.
   - Eliminates redundant/unaligned `FindGeomChunkOffset` scan heuristics.
   - Operates on strict buffer slice `buffer.Slice(metadata.GeomChunkOffset, metadata.GeomChunkSize)`.
   - Supports multi-chunk RCOL containers where Chunk 0 is a non-GEOM resource and Chunk 1 is a valid TS3 GEOM resource (`0x015A1849`), selecting the target GEOM chunk via candidate validation without chunk offset drift.

3. **Vertex Element Decoding Policy**:
   - Parses 9-byte vertex element descriptors (`DataType` uint32 LE, `Format` uint32 LE, `SizeBytes` byte) and computes exact vertex stride and element byte offsets:
     - **DataType 1 (Position)**: 3 x Float -> `MeshVector3(X, Y, Z)` (required).
     - **DataType 2 (Normal)**: 3 x Float -> `MeshVector3(X, Y, Z)`.
     - **DataType 3 (UV0 / UV1)**: 2 x Float -> `MeshVector2(U, V)` (first occurrence mapped to `Uv0`, second to `Uv1`).
     - **DataType 4 (Bone Indices)**: 4 x Byte -> bone indices array.
     - **DataType 5 (Bone Weights)**: 4 x Float or 3 x Float -> paired with DataType 4 to produce `IReadOnlyList<CanonicalBoneWeight>`.
     - **DataType 6 (Tangent)**: 3 or 4 x Float -> `MeshVector4(X, Y, Z, W)`.
     - **DataType 7 (TagValue)**: 4 x Byte -> metadata ignored.
     - **DataType 10 (Vertex ID)**: 4 x Byte -> metadata ignored.
     - **Unrecognized Datatypes**: Produces diagnostic warning issue (`MESHG003`). Missing Position descriptor produces warning issue (`MESHG004`).

4. **Index Buffer & Face Group Decoding Policy**:
   - Decodes Face Group Header (`groupMarker` uint32 LE 4B, `faceFormat` byte 1B, `facePointCount` uint32 LE 4B).
   - Reads 16-bit UInt16 index buffer entries and constructs `CanonicalFace(A, B, C)` triangle lists ($A, B, C$).

5. **Domain Model Instantiation & Canonical Mesh Validation**:
   - Instantiates `CanonicalMesh` (`Name`, `Vertices`, `Faces`, `Materials`, `CoordinateSystem.RightHandedYUp`, `SourceGameVersion.Sims3`, `Issues`).
   - Runs `ICanonicalMeshValidator.Validate(canonicalMesh)` to verify non-empty vertices/faces, index bounds (`MESHV003`), degenerate triangles (`MESHV004`), bone weight normalization ($[0.99, 1.01]$, `MESHV006`), and position coordinate validity (`MESHV005`).

6. **Error & Bounds Checking Matrix**:
   - **`MESHG000`**: Buffer/stream is null, empty, or unreadable.
   - **`MESHG001`**: Structural GEOM metadata read failure or out-of-bounds chunk slice.
   - **`MESHG002`**: Truncated vertex element descriptors, vertex buffer payload, face group header, or index buffer.
   - **`MESHG003`**: Diagnostic warning issue for unrecognized vertex descriptor datatype.
   - **`MESHG004`**: Diagnostic warning issue for missing required Position descriptor.

7. **Scope Boundaries**:
   - No TS4 binary writing, file format export, preview rendering, or UI logic in the importer service.

## Consequences
- Enables non-destructive, deterministic decoding of real TS3 GEOM meshes into game-agnostic canonical domain models.
- Guarantees strict chunk slice alignment with `Ts3GeomMetadataReader` across multi-chunk RCOL containers.
- Integrates canonical mesh validation into the import pipeline.
- Preserves existing decision records (`DEC-0024`, `DEC-0025` status Approved).
- Maintains quality gate compliance across solution projects.
