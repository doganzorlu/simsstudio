# DEC-0047: Capability Matrix UI Preflight Surface

- **Status**: Approved
- **Date**: 2026-09-09
- **Deciders**: SimsConverter Core Architecture Team
- **Task Reference**: SIMS-CONV-017 — Capability Matrix UI Preflight Surface

## Context & Problem Statement

Prior to executing a conversion between **TS3 -> TS4** or **TS4 -> TS3**, users need clear, preflight visual feedback regarding resource-level capabilities, supported counts, pass-through neutral metadata, and unsupported resources or omissions (`CAPA001` / `CAPA002`).

SimsConverter requires a dedicated **Capability Matrix Preflight Surface** in the UI (`ResourceInspectorViewModel` & Avalonia UI `MainWindow.axaml`), along with an active guard check on the `Convert` action button to prevent executing conversions when 0 supported object model/mesh/definition resources exist.

## Decision Drivers

1. **Preflight Surface Visibility**: Expose preflight resource capability summary counts (`Supported`, `PassThrough`, `Unsupported`) and resource-by-resource status table in a dedicated UI tab (`Capability Matrix Surface`).
2. **Resource-Level Warning Inspection**: Present `CAPA001` (known unsupported game tuning/script) and `CAPA002` (unrecognized TypeId) warnings in a dedicated preflight warnings panel.
3. **Controlled Action Guard**: Disable `Convert` action button (`CanConvert`) if preflight capability matrix evaluation indicates 0 supported resources (`CapabilitySupportedCount == 0`), preventing invalid or corrupt container conversion executions.
4. **Dynamic Direction Recalculation**: Immediately re-evaluate preflight capability matrix whenever `TargetGameVersion` or conversion direction changes (`TS3 -> TS4` <-> `TS4 -> TS3`).

## Technical Implementation

- **`ResourceInspectorViewModel` Properties**:
  - `CapabilityMatrix` (`DecorativeObjectConversionCapabilityMatrix?`)
  - `CapabilitySupportedCount` (`int`), `CapabilityPassThroughCount` (`int`), `CapabilityUnsupportedCount` (`int`)
  - `HasCapabilityMatrix` (`bool`), `HasCapabilityWarnings` (`bool`)
  - `CapabilityEntries` (`ObservableCollection<ConversionCapabilityEntry>`)
  - `CapabilityWarnings` (`ObservableCollection<ConversionIssue>`)
- **Action Guard Integration**: `CanConvert` evaluates `!(HasCapabilityMatrix && CapabilitySupportedCount == 0)`.
- **Avalonia UI Integration**: Added `Capability Matrix Surface` `TabItem` to `MainWindow.axaml` with banner summary cards, `DataGrid` capability table, and `CAPA001` / `CAPA002` warning ListBox.

## Consequences

- **Positives**: Complete visual preflight transparency prior to file conversion; proactive button guard preventing user frustration when attempting to convert non-convertable containers.
- **Verification**: Verified via ViewModel unit tests (`ResourceInspectorViewModelCapabilityTests`), 0 build warnings/errors, and 373 solution tests passing.
