# DEC-0001: Architecture Bootstrap & Solution Structure

- **Status**: Approved
- **Date**: 2026-08-27
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-BOOT-001

## Context
SIMSStudio requires a clean, scalable architecture to support Sims 3 / Sims 4 package inspection, 3D asset conversion, and UI presentation via Avalonia UI.

## Decision
We establish a multi-layered solution (`SimsConverter.sln`) targeting **.NET 10.0 (`net10.0`)** with strict dependency boundaries:

```
src/
  ├── SimsConverter.App            (UI Layer: Avalonia 12, MVVM, Composition Root)
  ├── SimsConverter.Application    (Application Layer: Orchestration & Use Cases)
  ├── SimsConverter.Domain         (Domain Layer: Core Models, Enums, Zero Dependencies)
  ├── SimsConverter.Infrastructure  (Infrastructure Layer: OS, Logging, File System)
  └── SimsConverter.Package        (Package Layer: DBPF Container Contracts & Inspection)

tests/
  ├── SimsConverter.Domain.Tests   (xUnit + FluentAssertions Tests for Domain Models)
  └── SimsConverter.Package.Tests  (xUnit + FluentAssertions Tests for Package Inspector Contracts)
```

### Dependency Rules
- `App` → `Application`, `Infrastructure`
- `Application` → `Domain`, `Package`
- `Infrastructure` → `Application`, `Domain`
- `Package` → `Domain`
- `Domain` → *No Dependencies*

### Framework & SDK Notes
- **Target Framework**: `.NET 10.0 (`net10.0`)`.
- **SDK**: Stable GA .NET 10 SDK (`10.0.400`), configured in [`global.json`](file:///Users/dogan/Documents/Projects/ownprojects/SIMSStudio/global.json).
- **Avalonia UI Version**: `12.0.5`.
- **Test Framework**: `xUnit` + `FluentAssertions`.

## Consequences
- Clean separation between binary parsing (`Package`), domain rules (`Domain`), and presentation (`App`).
- UI project contains zero binary parsing or Sims-specific file layout logic.
- DBPF container magic detection returns `GameVersion.Unknown` (no binary guessing).
- Full test coverage enabled via separate unit test projects with FluentAssertions.
