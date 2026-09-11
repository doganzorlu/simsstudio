# DEC-0023: Mesh Resource Raw Extraction Boundary Architecture

- **Status**: Approved
- **Date**: 2026-08-31
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-MESH-002-R1

## Context
Phase 3 of SIMSStudio requires establishing a safe raw payload extraction boundary service (`IMeshResourceExtractor` / `MeshResourceExtractor`) in `SimsConverter.Mesh` to export 3D geometry (`GEOM`, `MODL`, `MLOD`), morphs (`BGEO`), rigs (`RIG`), and slots (`RSLT`) without decoding binary buffers, performing vertex/index parsing, 3D rendering, or file format conversion.

## Decision

1. **Extraction Service Boundary (`IMeshResourceExtractor`)**:
   - `Task<MeshResourceExtractResult> ExtractAsync(MeshResourceExtractRequest request, CancellationToken cancellationToken = default)`

2. **Extraction Safety Guards**:
   - **KnownMesh Guard**: Export is strictly rejected if `Classification.Classification != MeshClassificationKind.KnownMesh` (`MESHE001`). `IPackageResourceExporter` is NOT invoked for unrecognized or invalid mesh candidates.
   - **ResourceId Identity Match Guard**: Export is strictly rejected if `Classification.ResourceId != Entry.Id` (`MESHE004`). `IPackageResourceExporter` is NOT invoked on identity mismatch.
   - **Filename Sanitization & Path Traversal Guard (`MESHE008`)**:
     - Custom file names are sanitized using `InvalidFileNameCharRegex` (`[\x00-\x1F\x7F\x22\x3C\x3E\x7C\x3A\x2A\x3F\x5C\x2F]`), replacing path separators (`/`, `\`), control characters, and illegal characters with underscores (`_`).
     - Target output path is canonicalized (`Path.GetFullPath`). Rejects export (`MESHE008`) if the target file path escapes `OutputDirectory` or is identical to `SourcePackagePath`.
   - **Input Validation**: Null or empty requests return controlled error issue `MESHE000`.

3. **Mandatory Byte Copy Delegation**:
   - `MeshResourceExtractor` MUST NOT implement custom stream reading or file byte-copy logic. All raw byte range copying is delegated to `IPackageResourceExporter`.
   - Preserves canonical path separation, overwrite policies (`AllowOverwrite`), and atomic temp file creation provided by the Package layer.

4. **Deterministic Extension & Filename Mapping**:
   - Extension mapping based on TypeId:
     - `0x015A1849` (`Ts3Geom`) -> `.geom`
     - `0x01661233` (`TsSharedModel`) -> `.modl`
     - `0x01D10F34` (`TsSharedModelLod`) -> `.mlod`
     - `0x8EAF13DE` (`TsSharedRig`) -> `.rig`
     - `0xD3044521` (`TsSharedSlot`) -> `.rslt`
     - `0x067CAA11` (`TsSharedBlendGeometry`) -> `.bgeo`
   - Default filename pattern: `"{TypeId:X8}_{GroupId:X8}_{InstanceId:X16}{extension}"`.

5. **Zero Binary Parsing or 3D Conversion**:
   - Performs zero vertex/index buffer decoding, GEOM/MODL/MLOD structure parsing, 3D preview rendering, or Assimp/SharpGLTF model translation.

## Consequences
- Establishes safe, isolated raw payload extraction for mesh resources.
- Prevents directory traversal attacks (`../`, absolute paths) and corrupt or mismatched resource exports via strict identity, sanitization, and path boundary guards.
- Maintains 100% test coverage and quality gate compliance across 6 solution projects.
