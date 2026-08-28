# SIMSStudio Developer & Architecture Documentation

Welcome to SIMSStudio. This project provides a multi-layered solution (`SimsConverter.sln`) for Sims package conversion, asset inspection, and container detection built on **.NET 10 (`net10.0`)** and **Avalonia UI 12**.

---

## 🛠️ Environment & SDK Setup

### Requirements
- **SDK**: .NET 10.0 Stable GA (`10.0.400`).
- **Configuration File**: Root [global.json](../global.json) enforces SDK version alignment.

---

## 🚀 Quality Gates & Verification Commands

Standard build and test validation commands:
```bash
dotnet build SimsConverter.sln
dotnet test SimsConverter.sln
```

---

## 📂 Architecture & Governance Index

- **Domain & Application Model**: [DOMAIN_MODEL.md](domain/DOMAIN_MODEL.md)
- **UI Contract Governance**: [UI_CONTRACT.md](ui-contract/UI_CONTRACT.md)
- **Sims3Pack Compatibility Validation Report**: [sims3pack_compatibility_report.md](reports/sims3pack_compatibility_report.md)

### Container & Format Support Matrix
- **DBPF `.package` (Sims 3 / Sims 4)**: Inspection & Raw Payload Export Supported
- **`.sims3pack` (Sims 3 Archive Wrapper)**: Container Detection, Secure XML Manifest Parsing, Embedded Archive Catalog Scanning, Embedded DBPF Raw Payload Export, UI Inspection Screen & Real Fixture Validation Harness Supported
- **Phase 2 Texture Pipeline**: Texture TypeId Catalog, Resource Candidate Classifier, Raw Texture Payload Extraction Boundary, Bounds-Checked DDS Binary Header Reader, DDS Payload Truncation Validator, Texture Inspection Application Service & Desktop UI Tab Screen Supported

### Key Decision Records
- [DEC-0001: Architecture Solution Bootstrap](decisions/DEC-0001-architecture-bootstrap.md)
- [DEC-0002: Package Detection Strategy](decisions/DEC-0002-package-detection.md)
- [DEC-0003: DBPF Header & Resource Index Parser](decisions/DEC-0003-dbpf-header-index-parser.md)
- [DEC-0004: Package Inspection Application Boundary](decisions/DEC-0004-package-inspection-application-boundary.md)
- [DEC-0005: Resource Inspector ViewModel Boundary](decisions/DEC-0005-resource-inspector-viewmodel-boundary.md)
- [DEC-0006: UI Contract Bootstrap for Avalonia Desktop](decisions/DEC-0006-ui-contract-bootstrap.md)
- [DEC-0007: DBPF Resource Inspector UI Screen Architecture](decisions/DEC-0007-resource-inspector-ui.md)
- [DEC-0008: Raw Resource Export Architecture & Atomic File Policy](decisions/DEC-0008-raw-resource-export.md)
- [DEC-0009: Sims3Pack Container Research & Detection Foundation](decisions/DEC-0009-sims3pack-container.md)
- [DEC-0010: Sims3Pack XML Metadata Section Parser Architecture](decisions/DEC-0010-sims3pack-xml-parser.md)
- [DEC-0011: Sims3Pack Embedded Payload Catalog Scanner Architecture](decisions/DEC-0011-sims3pack-catalog-scanner.md)
- [DEC-0012: Sims3Pack Embedded DBPF Payload Raw Exporter Architecture](decisions/DEC-0012-sims3pack-payload-exporter.md)
- [DEC-0013: Sims3Pack Application Boundary Service Architecture](decisions/DEC-0013-sims3pack-application-service.md)
- [DEC-0014: Sims3Pack UI Inspection Screen Architecture](decisions/DEC-0014-sims3pack-ui-screen.md)
- [DEC-0015: Sims3Pack Real Fixture Validation & Compatibility Matrix](decisions/DEC-0015-sims3pack-real-fixture-validation.md)
- [DEC-0016: Texture Resource Type Catalog & Candidate Classification Foundation](decisions/DEC-0016-texture-resource-classification.md)
- [DEC-0017: Texture Resource Raw Extraction Boundary Architecture](decisions/DEC-0017-texture-resource-extraction-boundary.md)
- [DEC-0018: DDS Header Parser & Metadata Reader Architecture](decisions/DEC-0018-dds-header-parser.md)
- [DEC-0019: DDS Payload Validation & Size Estimation Architecture](decisions/DEC-0019-dds-payload-validation.md)
- [DEC-0020: Texture Inspection Application Boundary Architecture](decisions/DEC-0020-texture-inspection-application-boundary.md)
