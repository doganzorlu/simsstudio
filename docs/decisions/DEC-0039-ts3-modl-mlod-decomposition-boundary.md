# DEC-0039: TS3 MODL/MLOD Object Model Decomposition Research & Boundary Architecture

- **Status**: Approved
- **Date**: 2026-09-02
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-CONV-003 / SIMS-CONV-003-R1 / SIMS-CONV-003-R2 / SIMS-CONV-003-R3 / SIMS-CONV-003-R4

## Context
Real-world TS3 decorative object packages (such as CC packages exported from `.sims3pack` files like the Onyx chopping board object) do not store raw direct TS3 GEOM (`0x015A1849`) resources; instead, their geometry is encapsulated within `MODL` (`0x01661233`) and `MLOD` (`0x01D10F34`) object model containers. To unblock Phase 4 decorative object conversion for MODL/MLOD-based packages, a read-only metadata reader boundary (`ITs3ObjectModelMetadataReader` / `Ts3ObjectModelMetadataReader`) is established in `SimsConverter.Mesh`.

## Decision

1. **Read-Only Metadata Reader Boundary (`SimsConverter.Mesh`)**:
   - Contract: `ITs3ObjectModelMetadataReader` (`Read(ReadOnlySpan<byte>, PackageResourceId)`, `Read(Stream, PackageResourceId)`, `ReadAsync(...)`).
   - Expects **decompressed** payload bytes.
   - Implementation: `Ts3ObjectModelMetadataReader` in `SimsConverter.Mesh.Services`.
   - Domain/Mesh Models: `Ts3ObjectModelMetadataResult`, `Ts3ObjectModelKind` (`Unknown`, `Modl`, `Mlod`), `Ts3ObjectModelLodInfo`, `Ts3ObjectModelGeometryReference`.

2. **Final Hardened Payload Decompression Policy (`SimsConverter.Package`)**:
   - Contract: `IPackageResourcePayloadReader` explicitly documents decoded/decompressed payload extraction for inspection and importers.
   - Uncompressed passthrough (returning raw buffers) is strictly restricted to `PackageCompressionKind.None` where `CompressedSize == DecompressedSize`.
   - `PackagePayloadDecompressor.TryDecompressZlib` performs mandatory Zlib decompression with **strict length matching** (`decomp.Length == entry.DecompressedSize`). Mismatches return controlled error `PKGP002`.
   - `PackageCompressionKind.RefPack` (or `0xFB10` header) payloads return controlled error `PKGP004` (`"RefPack (0xFB10) compression format is not supported; Zlib or uncompressed format required."`).
   - **Raw Export Integrity**: `Sims3PackPayloadExporter` reads raw binary byte ranges directly from source file streams (`DataOffset`, `CompressedSize`), ensuring raw archive export remains completely unaffected by payload decompression boundaries.

3. **Binary Header & Reference Parsing**:
   - Classifies resource `TypeId`: `0x01661233` $\rightarrow$ `Modl`, `0x01D10F34` $\rightarrow$ `Mlod`. Unrecognized TypeIds return `IsSuccess = false` with error `MODL002`.
   - Raw compressed headers (`0xFB10` / `0x78`) passed to `Ts3ObjectModelMetadataReader` return `IsSuccess = false` with error `MODL004`.
   - Seekable streams preserve stream position (`stream.Position` restored after reading).
   - Scans 16-byte TGI resource blocks for candidate GEOM (`0x015A1849`) and MLOD (`0x01D10F34`) references, labeling them explicitly as `"HeuristicGeometryCandidate"` or `"HeuristicMlodCandidate"`.

4. **Security & Boundary Guards**:
   - **Zero File Creation & Stream Position Preservation**: Read-only binary metadata extraction strictly operates on read-only byte spans or streams with position restoration.
   - **Quality Gate Compliance**: 290 passing unit tests across all project assemblies.

## Consequences
- Unblocks Phase 4 conversion planning for object model packages (MODL/MLOD) by establishing the binary metadata decomposition boundary.
- Guarantees stream position safety, raw export isolation, explicit RefPack error handling (`PKGP004`), and strict Zlib decompression length verification.
- Preserves 100% quality gate compliance with 0 build warnings and 290 passing unit tests across all project assemblies.
