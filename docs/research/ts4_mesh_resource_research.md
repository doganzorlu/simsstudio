# TS4 Mesh Resource Format Research & Type Boundary Specification

- **Author**: AI2 (Engineering Executor)
- **Task ID**: SIMS-MESH-008 / SIMS-MESH-009-R2
- **Date**: 2026-08-31
- **Status**: Completed / Aligned with TS4-SimRipper Reference

---

## 1. Executive Summary

This research specification establishes the authoritative type boundary and architectural foundation for *The Sims 4* (TS4) 3D mesh pipeline in `SIMSStudio`. Prior to implementing binary parsers or mesh importers for TS4, all TS4 mesh-related Resource Type IDs (TypeIds) have been analyzed, classified, and grounded against authoritative Sims 4 modding documentation and community tool repositories.

---

## 2. Authoritative Reference Grounding

Every TypeId in this specification is evaluated against verified primary and secondary sources:

1. **Primary Reference**: [TS4 Resource Type Index — The Sims 4 Modders Reference](https://thesims4moddersreference.org/reference/resource-types/)
2. **Secondary Reference (3D Sim Extraction Tooling)**: [TS4-SimRipper Repository — GEOM.cs Specification](https://github.com/technificentConsulting/TS4-SimRipper/blob/main/GEOM.cs)
3. **Secondary Reference (Blender Interop Specification)**: [Blender GEOM Tools v2.1.3 — ModTheSims](https://modthesims.info/d/656413/blender-geom-tools-v2-1-3-blender-2-8x-2-9x.html)

---

## 3. Verified TS4 Mesh Resource Catalog

### 3.1 Confirmed Production TypeIds

| Resource TypeId | Identifier | Classification | Role | Target Content | Citation |
| --- | --- | --- | --- | --- | --- |
| `0x015A1849` | **GEOM** / **TsSharedGeom** | **Confirmed** | Geometry | CAS Parts (Clothing, Hair, Accessories, Body) | [TS4 Modders Ref](https://thesims4moddersreference.org/), [TS4-SimRipper](https://github.com/technificentConsulting/TS4-SimRipper/blob/main/GEOM.cs) |
| `0x01661233` | **MODL** | **Confirmed** | ObjectMesh | Build/Buy Object Main Model Geometry | [TS4 Modders Ref](https://thesims4moddersreference.org/), ModTheSims |
| `0x01D10F34` | **MLOD** | **Confirmed** | ObjectLodMesh | Build/Buy Object Level-of-Detail Geometry | [TS4 Modders Ref](https://thesims4moddersreference.org/), ModTheSims |
| `0x8EAF13DE` | **RIG** | **Confirmed** | Rig / Skeleton | Joint Hierarchy / Skeleton Bone Data | [TS4 Modders Ref](https://thesims4moddersreference.org/), [TS4-SimRipper](https://github.com/technificentConsulting/TS4-SimRipper/blob/main/GEOM.cs) |
| `0xD3044521` | **RSLT** | **Confirmed** | Slot | Object Attachment / Animation Slot Layout | [TS4 Modders Ref](https://thesims4moddersreference.org/) |
| `0x067CAA11` | **BGEO** | **Confirmed** | Morph | Blend Geometry / Facial & Body Morph Sliders | [TS4 Modders Ref](https://thesims4moddersreference.org/) |

---

### 3.2 Candidate & Unconfirmed TypeIds

| Resource TypeId | Unconfirmed Label | Status | Policy | Notes |
| --- | --- | --- | --- | --- |
| `0x025C6425` | *Unverified MODL Variant* | **Unconfirmed** | Excluded from Production Constants | Unverified in core TS4 specifications; treated as `Unknown` (`MESHC001`) until sample package evidence is provided. |
| `0x02864C99` | *Unverified MLOD Variant* | **Unconfirmed** | Excluded from Production Constants | Unverified in core TS4 specifications; treated as `Unknown` (`MESHC001`) until sample package evidence is provided. |

---

## 4. Format Architecture & TS4 GEOM `GEOM.cs` Read Sequence

### 4.1 TS4 CAS `GEOM.cs` Reference Read Sequence
TS4 GEOM resources in `.package` files follow the RCOL container layout wrapping the internal GEOM chunk payload:

1. **RCOL Container Header**:
   - `version1` (`uint32`), `count` (`uint32`), `ind3` (`uint32`), `extCount` (`uint32`), `intCount` (`uint32`).
   - External TGIs (`extCount * 16` bytes).
   - Internal chunk locations (`intCount * 8` bytes: `abspos` 4B, `meshsize` 4B).
2. **Internal GEOM Chunk Payload at `abspos`**:
   - Magic: `"GEOM"` (4 bytes).
   - `version`: `uint32` (Supported: 5, 12, 13, 14).
   - `TGIoff`: `uint32` (offset to tail TGI list).
   - `TGIsize`: `uint32`.
   - `shaderHash`: `uint32`.
   - If `shaderHash != 0`: `MTNFsize` (`uint32`) + `MTNF` payload block (`MTNFsize` bytes).
   - `mergeGrp`: `int32`, `sortOrd`: `int32`.
   - `numVerts`: `uint32`, `Fcount`: `uint32` (vertex element descriptor count).
   - Vertex Descriptors: `Fcount` entries of 9 bytes (`datatype` 4B, `format` 4B, `size` 1B).
   - Vertex Buffer: `numVerts * stride` bytes.
   - Submesh Face Section: `numSubMeshes` (`uint32`), followed by submesh face buffers (`bytesperfacepnt` 1B + `numfacepoints` 4B + face index buffer).
   - Version Stitches & Slotrays:
     - If `version == 5`: `skconIndex` (`int32`).
     - If `version >= 12`: `uvStitchCount` (`int32`) + UV stitch block (`uvStitchCount * 8` B).
     - If `version >= 13`: `seamStitchCount` (`int32`) + seam stitch block (`seamStitchCount * 4` B).
     - `slotrayCount` (`int32`) + slotray block (`slotrayCount * 32` B).
   - Bone Section: `bonehashcount` (`int32`) + bone hashes (`bonehashcount * 4` bytes).
   - Tail TGI List (at `TGIoff`): `numtgi` (`int32`) + TGI entries (`numtgi * 16` bytes).

---

## 5. Implementation Progress

- **Phase 3.1 Implemented Importer**: **TS4 CAS GEOM (`0x015A1849` / `TsSharedGeom`) CanonicalMesh Importer (`SIMS-MESH-010`)**
  - Implemented `ITs4GeomCanonicalMeshImporter` / `Ts4GeomCanonicalMeshImporter` providing full binary vertex and index buffer decoding into `CanonicalMesh` entities (`SourceGameVersion = Sims4`, `CoordinateSystem = RightHandedYUp`).
- **Recommended Entry Point (Option A)**: **TS4 CAS GEOM (`0x015A1849`) Importer**, extending the candidate-validated RCOL chunk slice importer established in `SIMS-MESH-005-R1`.
