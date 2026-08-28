# DEC-0003: DBPF Header & Resource Index Parser Architecture

- **Status**: Approved
- **Date**: 2026-08-27
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-PKG-002

## Context
SIMSStudio requires a safe, robust DBPF binary parser (`IDbpfPackageParser` / `DbpfPackageParser`) capable of reading DBPF container headers and extracting resource index entry tables across DBPF 1.x and DBPF 2.0 specifications.

## Layout Mapping & Endianness Specification

### 1. Header Layout Field Mapping (96-Byte Minimum Header Buffer)
All multi-byte numeric fields are encoded in **Little Endian**:

| Field Name | Offset (DBPF 2.0 / Sims 4) | Offset (DBPF 1.x / Sims 3) | Data Type | Description |
| --- | --- | --- | --- | --- |
| `Magic` | 0..4 | 0..4 | ASCII String | DBPF Magic Identifier ("DBPF") |
| `MajorVersion` | 4..8 | 4..8 | Int32 (LE) | DBPF Major Version (1 or 2) |
| `MinorVersion` | 8..12 | 8..12 | Int32 (LE) | DBPF Minor Version (0 or 1) |
| `IndexEntryCount` | 36..40 | 24..28 | Int32 (LE) | Total number of index entries |
| `IndexOffset` | 40..44 | 32..36 | Int32 / Int64 (LE) | Byte offset to resource index table |
| `IndexSizeBytes` | 44..48 | 36..40 | Int32 (LE) | Total size in bytes of index table |

*(This explicit layout documentation closes the residual backlog from DEC-0002 regarding index entry count and offset spec assumptions).*

### 2. Resource Index Entry Table Layout

#### DBPF 2.0 (32 Bytes per Entry)
- `TypeId`: Offset 0..4 (UInt32 LE)
- `GroupId`: Offset 4..8 (UInt32 LE)
- `InstanceId`: Offset 8..16 (UInt64 LE, composed of InstanceEx + Instance)
- `DataOffset`: Offset 16..20 (UInt32 LE)
- `CompressedSize`: Offset 20..24 (UInt32 LE)
- `DecompressedSize`: Offset 24..28 (UInt32 LE)
- `CompressionFlags`: Offset 28..30 (UInt16 LE) — `0x0000` = None, `0x5A42` = Zlib, `0xFFFE` = RefPack.

#### DBPF 1.x (20 Bytes per Entry)
- `TypeId`: Offset 0..4 (UInt32 LE)
- `GroupId`: Offset 4..8 (UInt32 LE)
- `InstanceId`: Offset 8..12 (UInt32 LE, widened to ulong)
- `DataOffset`: Offset 12..16 (UInt32 LE)
- `CompressedSize`: Offset 16..20 (UInt32 LE)
- `DecompressedSize`: Offset 16..20 (UInt32 LE)
- `CompressionFlags`: 0 (None)

## Strategic Principles & Guard Rules
1. **Guarded Binary Reads & Bounds Checks**:
   - Every slice operation is guarded by exact length checks before invocation. Buffers of 4..7 bytes (e.g. "DBPF" magic only or truncated version bytes) do NOT invoke `buffer.Slice(4, 4)` and return a safe partial header with diagnostic warning (`PARSE003`) without throwing `ArgumentOutOfRangeException`.
   - Index table size consistency is strictly validated: `IndexSizeBytes` must be greater than or equal to `IndexEntryCount * EntrySize` (`PARSE013`).
2. **Stream Compatibility**:
   - For non-seekable streams (`!stream.CanSeek`), the parser advances to forward `indexOffset` by reading and discarding intermediate bytes. If backward seeking is required, it returns a controlled error (`PARSE014`) without crashing.
3. **Typed Value Objects**: Resource IDs are strictly modeled via `PackageResourceId` (TypeId, GroupId, InstanceId) with formatted hex key representations (`FormattedKey`).
4. **No Binary Version Guessing**: Parsing DBPF headers does NOT attempt to classify the package as Sims 3 or Sims 4. Game version remains `GameVersion.Unknown`.
5. **Non-Destructive Operations**: Files are accessed read-only (`FileShare.Read`).
