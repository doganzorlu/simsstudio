# DEC-0008: Raw Resource Export Architecture & Atomic File Policy

- **Status**: Approved
- **Date**: 2026-08-27
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-PKG-006

## Context
SIMSStudio requires a safe foundation for exporting raw binary resource payloads from DBPF package containers. The export operation must extract byte ranges accurately without modifying the source container, without decompressing or interpreting payload contents, and without risking corrupted partial writes or accidental file overwrites.

## Decision

1. **Non-Destructive Source Package Access & Overwrite Prohibition**:
   - Source package files are opened with `FileShare.Read` and `FileAccess.Read`. Source package files are NEVER modified or written to under any circumstances.
   - **Canonical Path Protection Guard (`EXPE008`)**: `PackageResourceExporter.ExportAsync` canonicalizes source and output paths using `Path.GetFullPath`. If the target output path resolves to the same file as the source package file path, the request is immediately rejected with controlled error `EXPE008`, regardless of whether `AllowOverwrite` is `true`.

2. **Atomic Temp File Writing & Overwrite Guard**:
   - Resource byte ranges are written to a temporary file (`${outputPath}.tmp.${guid}`) first.
   - Upon successful stream copy and size validation, the temporary file is atomically moved to the destination path using `File.Move(..., overwrite: AllowOverwrite)`.
   - `AllowOverwrite` strictly defaults to `false` in `ResourceInspectorViewModel` and `ResourceExportService`. Target overwrites require explicit user selection in UI (`Overwrite Existing` CheckBox). Existing target files without overwrite return controlled error `EXPE005`.
   - Temporary files are guaranteed to be cleaned up on all failure paths (`EXPE007` premature EOS, exceptions, cancellations).

3. **Overflow-Safe Bounds Checking & Error Diagnostics**:
   - Validates bounds using arithmetic overflow safety: `request.Offset < 0 || request.Offset > fileLength || request.CompressedSize > (fileLength - request.Offset)`.
   - Out-of-bounds requests return a controlled `PackageResourceExportResult.Failure` (`EXPE006`) without throwing uncaught exceptions.

4. **Robust Cross-Platform Filename Sanitization**:
   - The application layer (`ResourceExportService.SanitizeFilename`) sanitizes filenames by replacing path separators (`/`, `\`), path navigation tokens (`..`), OS-specific invalid characters (`Path.GetInvalidFileNameChars()`), and cross-platform invalid characters (`* ? " < > | :`).

5. **UI & ViewModel Layer Separation**:
   - `ResourceInspectorViewModel` consumes `IResourceExportService` and `IFilePickerService.OpenFolderPickerAsync()`.
   - `SimsConverter.App` contains ZERO file stream or byte copying code, verified by automated source scan unit tests.

## Consequences
- Guarantees data integrity of source `.package` files during resource export operations under all circumstances.
- Prevents partial, corrupt, or self-destructive file overwrites in destination directories.
- Protects user files from accidental overwrites.
- Provides a reusable foundation for future texture/mesh exporters and converters in EPIC-004.
