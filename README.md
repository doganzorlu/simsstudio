# SIMSStudio — Sims 3 & Sims 4 Asset Conversion & Package Inspector

SIMSStudio is a high-performance desktop tool for Sims 3 and Sims 4 package inspection, 3D asset conversion, and DBPF container analysis.

---

## 🏗 Architecture Overview

The solution follows a multi-layered clean architecture:

- **SimsConverter.App**: Avalonia UI presentation layer and application composition root. Zero binary parsing logic.
- **SimsConverter.Application**: Application services and use-case orchestration interfaces.
- **SimsConverter.Domain**: Core domain entities (`GameVersion`, `ConversionIssue`, `ConversionReport`) with zero external dependencies.
- **SimsConverter.Infrastructure**: Technical infrastructure adapters (file system, OS integration, logging).
- **SimsConverter.Package**: DBPF package layout inspectors and low-level container contracts.

See [DEC-0001 Architecture Bootstrap Decision Record](docs/decisions/DEC-0001-architecture-bootstrap.md) for detailed architectural decisions.

---

## ⚙️ Requirements & Environment Setup

- **Target Framework**: .NET 10 (`net10.0`)
- **SDK**: .NET 10.0 Stable GA (`10.0.400`)
- **UI Framework**: Avalonia UI 12 (`12.0.5`)
- **SDK Alignment**: Configured via root [global.json](global.json)

---

## 🚀 Quality Gates & Verification Commands

To build and run automated unit tests:

```bash
dotnet build SimsConverter.sln
dotnet test SimsConverter.sln
```

To run the Avalonia UI application:

```bash
dotnet run --project src/SimsConverter.App/SimsConverter.App.csproj
```

---

## 📚 Documentation Links

- [Domain Model Architecture](docs/domain/DOMAIN_MODEL.md)
- [Database Metadata & Policy](docs/domain/DB_META.md)
- [DEC-0001 Architecture Bootstrap Decision Record](docs/decisions/DEC-0001-architecture-bootstrap.md)
- [AI Governance Rules](docs/AI_Governance/AGENT_BOOTSTRAP.md)
