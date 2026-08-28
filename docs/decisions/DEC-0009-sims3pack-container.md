# DEC-0009: Sims3Pack Binary Header Research & Detection Foundation

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-S3P-001-R2

## Context
SIMSStudio requires accurate container-level detection of `.sims3pack` files based on the authoritative binary outer container header specification. Format detection is based on the community format specification ([ModTheSims Sims3Pack Container Specification](https://modthesims.info/wiki.php?title=Sims3Pack)).

## Research Findings & Technical Specifications

### 1. Outer Container Binary Header Layout
A valid `.sims3pack` container consists of an outer binary header followed by an XML metadata section, thumbnail preview resources, and embedded DBPF `.package` files:

| Field Name | Type | Size (Bytes) | Description |
| --- | --- | --- | --- |
| `signatureLength` | `DWORD` (`uint` LE) | 4 | Length of signature string (typically 8 for `"TS3Pack\0"` or 7 for `"TS3Pack"`) |
| `signature` | `ASCII string` | `signatureLength` | Must strictly equal `"TS3Pack"` (or null-terminated `"TS3Pack\0"`) |
| `version` | `WORD` (`ushort` LE) | 2 | Container format header version (typically `1`) |
| `xmlLength` | `DWORD` (`uint` LE) | 4 | Length of embedded XML metadata section in bytes |
| `xmlSection` | `UTF-8 string` | `xmlLength` | Starts at `headerLength = 4 + signatureLength + 2 + 4` (offset 17 or 18) |

### 2. Signature & Length Bounds Validation Rules
- **High Confidence (`Sims3Pack`, `GameVersion.Sims3`, `High`)**:  
  Confirmed ONLY when:
  1. Valid binary `TS3Pack` signature (`signatureLength` between 7 and 64, exact signature `"TS3Pack"` or `"TS3Pack\0"`) is present.
  2. `xmlLength` bounds check `(headerLength + xmlLength) <= stream.Length` passes for seekable streams.
  3. Valid XML preamble (`<?xml` or `<`) is verified at calculated offset `headerLength`.
- **Strict Signature Match**:  
  Strings beginning with `"TS3Pack"` but containing extra trailing characters (e.g. `"TS3PackInvalid"`) MUST NOT be accepted as valid signatures. Returns `Low` confidence with issue `DET006`.
- **`xmlLength` Stream Bounds Guard (`DET011`)**:  
  If `headerLength + xmlLength > stream.Length`, the container is flagged with controlled issue `DET011` and confidence is capped at `Low`.
- **Non-Seekable Stream Guard (`DET012`)**:  
  If `stream.CanSeek == false`, total stream length cannot be verified against `xmlLength`. `DetectAsync` executes safely without touching `stream.Length` or throwing `NotSupportedException`, capping confidence at `Low` with issue `DET012`.
- **Plain XML Text Files (`DET008`)**:  
  Files containing plain text XML preambles without binary `TS3Pack` outer headers return `Confidence.Low` with controlled warning issue `DET008`.
- **DBPF Masquerade Guard (`DET005`)**:  
  If a `.sims3pack` file starts with binary `"DBPF"` magic header, it is flagged as `PackageContainerKind.Dbpf` with a controlled warning (`DET005`), preventing improper parsing.

### 3. Scope Boundaries & Future Work
- **Embedded DBPF Extraction**:  
  Parsing internal section tables and extracting embedded `.package` files is marked as **Not Implemented / Foundation Stub** in this task. Embedded payload extraction will be implemented in task `SIMS-S3P-002`.
- **Zero UI Leakage**:  
  No presentation code or binary parsing additions were made to `SimsConverter.App`.

## Decision
1. Replaced XML-first detection in `Sims3PackDetector` with binary Little-Endian header parsing (`signatureLength`, `"TS3Pack"`, `version`, `xmlLength`).
2. Hardened bounds check `(headerLength + xmlLength) <= stream.Length` and strict signature matching.
3. Added non-seekable stream protection in `Sims3PackDetector.DetectAsync`.
4. Updated `Sims3PackDetectorTests` with synthetic binary `TS3Pack` header fixtures, `"TS3Pack\0"` fixtures, signature spoof tests, `xmlLength` overflow tests, and non-seekable stream wrappers.

## Consequences
- Guarantees accurate format detection aligned with the ModTheSims Sims3Pack specification.
- Prevents plain XML text files or spoofed headers from falsely producing High confidence container results.
- Protects stream inspection against uncaught `NotSupportedException` errors on non-seekable input streams.
- Provides a clean foundation for upcoming embedded package extraction tasks.
