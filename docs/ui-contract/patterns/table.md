# UI Contract Pattern: Resource Inspector Table & List Grid

This document defines DBPF resource inspector table and data grid layout standards.

---

## 📋 Required Columns

Package resource inspection tables MUST expose the following presentation fields:

| Column Name | ViewModel Property | Alignment | Font Style | Format Example |
| --- | --- | --- | --- | --- |
| **Formatted Key** | `FormattedKey` | Left | Monospaced | `00B2D882:00000000:123456789ABCDEF0` |
| **Type** | `TypeHex` | Left | Monospaced | `0x00B2D882` |
| **Group** | `GroupHex` | Left | Monospaced | `0x00000000` |
| **Instance** | `InstanceHex` | Left | Monospaced | `0x123456789ABCDEF0` |
| **Offset** | `Offset` | Right | Regular | `500` |
| **Compressed Size**| `CompressedSize` | Right | Regular | `1024 B` |
| **Decompressed Size**|`DecompressedSize`| Right | Regular | `2048 B` |
| **Compression** | `CompressionName` | Center | SemiBold Badge | `Zlib` / `None` / `RefPack` |

---

## 🚫 Rules
1. **Preserve Entry Order**: Table items MUST preserve the index entry order returned by the parser.
2. **Monospaced Key Alignment**: Hexadecimal keys must use monospaced fonts to guarantee column vertical alignment.
