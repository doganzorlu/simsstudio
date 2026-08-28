# DEC-0020: Texture Inspection Application Boundary Architecture

- **Status**: Approved
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-TEX-005

## Context
Phase 2 of SIMSStudio requires an Application layer texture inspection boundary (`ITextureInspectionService` / `TextureInspectionService`) in `SimsConverter.Application` to translate package inspection entries into UI-ready presentation models (`TextureResourceRow`) and determine export/parse capability flags without executing binary byte decoding or DDS header parsing in the Application layer.

## Decision

1. **Clean Application Presentation Models**:
   - `TextureResourceRow` encapsulates formatted hex keys (`TypeHex`, `GroupHex`, `InstanceHex`, `FormattedKey`), classification format names, map kind roles, game versions, capability flags (`CanExtractRawPayload`, `CanParseDdsHeader`), and aggregated issues.

2. **Capability Flag Logic**:
   - `CanExtractRawPayload == true` strictly when `ClassificationKind == TextureClassificationKind.KnownTexture`.
   - `CanParseDdsHeader == true` strictly when `ClassificationKind == TextureClassificationKind.KnownTexture` AND format is DDS (`Ts3DdsTexture` / `0x00B2D882`). Non-DDS texture formats (e.g. RLE2, LRLE) set `CanParseDdsHeader = false`.

3. **No Silent Omission of Unknown Resources**:
   - Resources with unrecognized TypeIds are NOT filtered out or omitted. They are projected as `TextureResourceRow` with `ClassificationKind = Unknown` and include diagnostic warning issue `TEXC001`.

4. **Zero Binary Byte Parsing in Application Layer**:
   - `TextureInspectionService` operates strictly on domain/application models (`PackageResourceEntry`, `PackageResourceRow`, `TextureResourceClassification`) and delegates classification to `ITextureResourceClassifier`. It performs zero binary byte manipulations or file stream reads.

5. **DI Container Wireup in `App.axaml.cs`**:
   - `ITextureInspectionService` is registered in `SimsConverter.App` DI composition root without adding UI controls or modifying ViewModel bindings.

## Consequences
- Establishes a clean, decoupled Application boundary for texture inspection.
- Prepares the presentation layer for Phase 2 texture UI screens.
- Maintains 100% test coverage and quality gate compliance across 5 solution projects.
