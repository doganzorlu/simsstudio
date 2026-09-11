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
- **Phase 3 Mesh Pipeline**: Mesh TypeId Catalog, Resource Candidate Classifier, Raw Mesh Payload Extraction Boundary, Game-Agnostic Canonical Mesh Domain Model Foundation & TS3 GEOM Binary Structural Metadata Reader Supported

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
- [DEC-0021: Texture Inspection UI Integration Architecture](decisions/DEC-0021-texture-inspection-ui-integration.md)
- [DEC-0022: Mesh Resource Type Catalog & Candidate Classification Architecture](decisions/DEC-0022-mesh-resource-classification.md)
- [DEC-0023: Mesh Resource Raw Extraction Boundary Architecture](decisions/DEC-0023-mesh-resource-extraction-boundary.md)
- [DEC-0024: Canonical Mesh Domain Model Foundation Architecture](decisions/DEC-0024-canonical-mesh-domain-model.md)
- [DEC-0025: TS3 GEOM Header & Structural Metadata Reader Architecture](decisions/DEC-0025-ts3-geom-metadata-reader.md)
- [DEC-0026: TS3 GEOM Vertex & Index Decoder to CanonicalMesh Importer Architecture](decisions/DEC-0026-ts3-geom-canonical-mesh-importer.md)
- [DEC-0027: Mesh Inspection Application Boundary Service Architecture](decisions/DEC-0027-mesh-inspection-application-boundary.md)
- [DEC-0028: Mesh Inspection UI Integration Architecture](decisions/DEC-0028-mesh-inspection-ui-integration.md)
- [DEC-0029: TS4 Mesh Resource Format Research & Type Boundary Architecture](decisions/DEC-0029-ts4-mesh-resource-boundary.md)
- [DEC-0030: TS4 CAS GEOM Metadata Reader Foundation Architecture](decisions/DEC-0030-ts4-cas-geom-metadata-reader.md)
- [DEC-0031: TS4 CAS GEOM Vertex & Index Decoder to CanonicalMesh Importer Architecture](decisions/DEC-0031-ts4-geom-canonical-mesh-importer.md)
- [DEC-0032: TS4 Canonical Mesh Inspection Application Integration Architecture](decisions/DEC-0032-ts4-mesh-inspection-application-integration.md)
- [DEC-0033: Phase 3 Mesh Real Fixture Validation & Compatibility Matrix Architecture](decisions/DEC-0033-mesh-real-fixture-validation.md)
- [DEC-0034: Decorative Object Conversion Boundary & Planning Skeleton Architecture](decisions/DEC-0034-decorative-object-conversion-boundary-skeleton.md)
- [DEC-0035: TS3 Decorative Object Source Asset Graph Builder Architecture](decisions/DEC-0035-decorative-object-source-asset-graph.md)
- [DEC-0036: DBPF Index Sanity Validation & UI Candidate Count Accuracy Architecture](decisions/DEC-0036-dbpf-index-sanity-validation-candidate-count-accuracy.md)
- [DEC-0037: Sims3Pack Embedded DBPF Payload Validation & Export Boundary Hardening Architecture](decisions/DEC-0037-sims3pack-embedded-dbpf-payload-validation-and-export-boundary-hardening.md)
- [DEC-0038: Real Sims3Pack Embedded DBPF Implicit Layout Compatibility Architecture](decisions/DEC-0038-real-sims3pack-embedded-dbpf-layout-compatibility.md)
- [DEC-0039: TS3 MODL/MLOD Object Model Decomposition Research & Boundary Architecture](decisions/DEC-0039-ts3-modl-mlod-decomposition-boundary.md)

## Reports & Specifications
- [Phase 3 Mesh Pipeline Compatibility Matrix & Real Fixture Validation Report](reports/mesh_compatibility_report.md)
