# DEC-0005: Resource Inspector ViewModel & DI Boundary Architecture

- **Status**: Decision Drafted / Pending Approval
- **Date**: 2026-08-27
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-PKG-004

## Context
SIMSStudio requires a clean MVVM presentation boundary (`ResourceInspectorViewModel`) and Dependency Injection composition root in `SimsConverter.App` to enable package container inspection in Avalonia UI without violating clean layer boundaries or introducing direct binary parsing into presentation code.

## Decision

1. **Dependency Injection Composition Root**:
   - `Microsoft.Extensions.DependencyInjection` is configured at startup in `App.axaml.cs`:
     - `IDbpfPackageParser` -> `DbpfPackageParser` (Singleton)
     - `IPackageInspectionService` -> `PackageInspectionService` (Singleton)
     - `ResourceInspectorViewModel` (Transient)
   - `MainWindow.DataContext` is set via DI composition root during application startup.

2. **MVVM State Model (`ResourceInspectorViewModel`)**:
   - Built on `CommunityToolkit.Mvvm` (`ObservableObject`, `[RelayCommand]`).
   - Maintains explicit UI states:
     - `SelectedFilePath`: Observable string bound to file path text input.
     - `IsBusy`: Boolean state indicating active background inspection.
     - `StatusMessage`: Human-readable status output (ready, inspecting, success, error).
     - `Resources`: `ObservableCollection<PackageResourceRow>` for UI grid/list binding.
     - `Issues`: `ObservableCollection<ConversionIssue>` tracking diagnostic warnings/errors.
     - `CanInspect`: Guard property (`!IsBusy && !IsNullOrWhiteSpace(SelectedFilePath)`).

3. **Strict Binary Isolation**:
   - `SimsConverter.App` contains ZERO binary byte offset parsing and ZERO references to `System.Buffers.Binary.BinaryPrimitives`.
   - Inspection logic is delegated entirely to `IPackageInspectionService`.

4. **Minimal UI Binding (`MainWindow.axaml`)**:
   - Semantic XAML binding with text input, progress bar, command button, status message, and list view without complex custom styling or overrides.

## Consequences
- ViewModels can be thoroughly tested in isolation (`SimsConverter.App.Tests`) without Avalonia UI headless rendering.
- UI elements bind cleanly to `PackageResourceRow` view objects without binary knowledge.
