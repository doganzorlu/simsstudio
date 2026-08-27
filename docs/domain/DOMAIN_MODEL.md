# Domain Model Architecture

This document defines the core domain models and module capabilities for SIMSStudio.

## Modules

### 1. SimsConverter.Domain
Contains core domain models, enums, and business entities that represent conversion concepts across Sims game versions.

#### Key Entities & Types
- **GameVersion**: Enum representing supported Sims game versions (`Sims3`, `Sims4`, `Unknown`).
- **ConversionIssueSeverity**: Enum representing diagnostic severity (`Info`, `Warning`, `Error`, `Fatal`).
- **ConversionIssue**: Immutable record tracking a specific issue encountered during analysis/conversion.
- **ConversionReport**: Aggregate tracking all issues, source/target versions, execution timestamp, and overall error status (`HasErrors`).

### 2. SimsConverter.Package
Contains contracts and data structures for DBPF and package container inspection without binding to UI or rendering logic.

#### Key Entities & Contracts
- **PackageHeaderSummary**: Record summarizing index count, DBPF versioning, and detected game version.
- **IPackageInspector**: Interface for package header inspection.

---

## Strategic Design Rules
1. `SimsConverter.Domain` MUST NOT depend on any external libraries or other projects in the solution.
2. Domain entities must remain immutable or encapsulate state changes strictly.
