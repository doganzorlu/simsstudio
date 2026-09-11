# SIMSStudio UI Contract Governance (Avalonia Desktop)

This document defines the master design system and UI contract for SIMSStudio desktop application built on **Avalonia UI 12** and **.NET 10**.

---

## 🏛️ Core Architectural Rules
1. **Zero Binary Parsing in UI**: Presentation components (`SimsConverter.App` ViewModels, Views, Controls) MUST NOT contain low-level binary parsing or byte offset manipulation (`System.Buffers.Binary.BinaryPrimitives`). Inspection and parsing logic must be executed via `IPackageInspectionService`, `ITextureInspectionService`, or `IMeshInspectionService`.
2. **Transparent Diagnostic Reporting**: UI components MUST report diagnostic issues (`ConversionIssue`) returned by lower layers without swallowing exceptions, displaying zero-byte dummy fallbacks, or masking corrupt container states.
3. **Semantic Token Policy**: Layout elements must bind to semantic theme resources rather than hardcoded inline hex colors or arbitrary static offsets.
4. **Asynchronous Non-Blocking Execution**: Long-running inspection or conversion workflows must execute asynchronously without blocking the Avalonia UI main looper thread.
5. **Texture Candidates Tab Pattern**: Package resource inspection UI MUST present texture candidates in a dedicated `TabControl` tab ("Texture Candidates"), exposing formatted hex keys (`CellStyleClasses="monospaced"`), format names, map kind roles, game versions, capability flags (`CanExtractRawPayload`, `CanParseDdsHeader`), and issue counts without performing texture byte extraction or binary DDS header parsing in the UI.
6. **Mesh Candidates Tab Pattern**: Package resource inspection UI MUST present mesh candidates in a dedicated `TabControl` tab ("Mesh Candidates"), exposing formatted hex keys (`CellStyleClasses="monospaced"`), format names, mesh roles, game versions, capability flags (`CanExtractRawPayload`, `CanInspectCanonicalMesh`), and canonical mesh decoding summary fields (`VertexCount`, `FaceCount`, `BoneCount`, `HasNormals`, `HasUv0`, `HasBoneWeights`, `Issues.Count`) without performing binary GEOM decoding or stream slicing in the UI assembly.

---

## 🗂️ Master Document Index

### Foundations
- [Colors](foundations/colors.md) — Semantic color tokens, status severity brushes, theme resource usage.
- [Typography](foundations/typography.md) — Desktop density typography scale, font weights, monospaced hex keys.
- [Spacing](foundations/spacing.md) — Compact desktop grid offsets, padding, margin standards.
- [Motion](foundations/motion.md) — Subtle desktop transitions and loading indicator policies.

### Patterns
- [Layout](patterns/layout.md) — App shell, panel splitting, inspector master-detail layouts.
- [Dashboard](patterns/dashboard.md) — Module landing surfaces and summary dashboards.
- [CRUD Screen](patterns/crud-screen.md) — Data management screens and placeholder standards.
- [Form](patterns/form.md) — Path input controls, validation states, inspect action triggers.
- [Table](patterns/table.md) — DBPF resource inspector data grids, column alignments, offset displays.
- [Empty State](patterns/empty-state.md) — Empty container states, unselected file placeholders, error states.
- [Modal & Drawer](patterns/modal-drawer.md) — Dialog windows, modal drawers for resource details and export.

### Components
- [Button](components/button.md) — Primary, secondary, icon buttons and disabled command states.
- [Badge](components/badge.md) — Severity badges (Info, Warning, Error, Fatal) and compression tags.
- [Input](components/input.md) — File path text boxes, placeholder text, validation styles.
- [Dropdown](components/dropdown.md) — Resource category filtering and non-eager loading policies.

### Governance
- [Override Rules](governance/override-rules.md) — Process for requesting UI overrides and theme deviations.
- [Deviation Log](governance/deviation-log.md) — Initial empty log tracking approved design deviations.

---

## 📋 Bootstrap Checklist
- [x] Solution bootstrapped with Avalonia UI 12 and .NET 10 (`net10.0`).
- [x] Dependency injection composition root configured in `App.axaml.cs`.
- [x] Clean MVVM separation (`ResourceInspectorViewModel` in `SimsConverter.App`).
- [x] Zero binary parsing in UI assembly verified via unit tests (`SimsConverter.App.Tests`).
- [x] UI contract governance structure established.
- [x] Texture inspection tab integration verified (`TextureResources`, `CanExtractSelectedTexture`, `CanParseSelectedDdsHeader`).
- [x] Mesh inspection tab integration verified (`MeshResources`, `CanInspectSelectedMesh`, `SelectedMeshResource`).
