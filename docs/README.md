# SIMSStudio Developer & Architecture Documentation

Welcome to SIMSStudio. This project provides a multi-layered solution (`SimsConverter.sln`) for Sims package conversion and asset inspection built on **.NET 10 (`net10.0`)** and **Avalonia UI 12**.

---

## 🛠️ Environment & SDK Setup

### Requirements
- **SDK**: .NET 10.0 SDK (`10.0.100-rc.2.25502.107` or higher).
- **Configuration File**: Root [`global.json`](file:///Users/dogan/Documents/Projects/ownprojects/SIMSStudio/global.json) enforces SDK version alignment.

### Developer Environment PATH Setup
If `.NET 10 SDK` was installed via `dotnet-install.sh` under `~/.dotnet`:
```bash
export PATH="$HOME/.dotnet:$PATH"
```

---

## 🚀 Quality Gates & Verification Commands

Standard build and test validation commands:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build SimsConverter.sln
dotnet test SimsConverter.sln
```

---

## 📂 Solution Structure & Layer Rules

See [DEC-0001-architecture-bootstrap.md](file:///Users/dogan/Documents/Projects/ownprojects/SIMSStudio/docs/decisions/DEC-0001-architecture-bootstrap.md) for architectural decisions and dependency boundaries.
