# Domain & Application Architecture

This document defines the core domain models, application use cases, and module capabilities for SIMSStudio.

## Modules

### 1. SimsConverter.Domain
Contains core domain models, enums, and business entities that represent conversion concepts across Sims game versions.

#### Key Entities & Types
- **GameVersion**: Enum representing supported Sims game versions (`Sims3`, `Sims4`, `Unknown`).
- **PackageContainerKind**: Enum representing recognized package container formats (`Unknown`, `Dbpf`, `Sims3Pack`).
- **PackageDetectionConfidence**: Enum representing confidence level of detection (`None`, `Low`, `High`).
- **PackageCompressionKind**: Enum representing entry compression algorithm (`Unknown`, `None`, `Zlib`, `RefPack`).
- **Sims3PackPayloadKind**: Enum representing scanned payload candidate type (`Unknown`, `DbpfPackage`, `PngPreview`).
- **PackageResourceId**: Value object record representing resource identification tuple (`TypeId` uint, `GroupId` uint, `InstanceId` ulong, with formatted key representation).
- **ConversionIssueSeverity**: Enum representing diagnostic severity (`Info`, `Warning`, `Error`, `Fatal`).
- **ConversionIssue**: Immutable record tracking a specific issue encountered during analysis/conversion.
- **ConversionReport**: Aggregate tracking all issues, source/target versions, execution timestamp, and overall error status (`HasErrors`).
- **PackageDetectionResult**: Immutable record containing detection outcomes (`ContainerKind`, `DetectedGameVersion`, `Confidence`, versioning, issues).
- **DbpfHeader**: Record containing parsed DBPF header layout metadata (`Magic`, `MajorVersion`, `MinorVersion`, `IndexEntryCount`, `IndexOffset`, `IndexSizeBytes`).
- **PackageResourceEntry**: Immutable record representing a parsed resource entry (`PackageResourceId Id`, `DataOffset`, `CompressedSize`, `DecompressedSize`, `PackageCompressionKind`, `CompressionFlags`).
- **DbpfParseResult**: Immutable record containing parse operation status (`IsSuccess`, `DbpfHeader?`, `Entries`, `Issues`).
- **PackageResourceExportRequest**: Value record specifying source package path, offset, size, target output path, and overwrite policy (`AllowOverwrite`).
- **PackageResourceExportResult**: Value record specifying export operation status (`IsSuccess`), source path, output path, exported byte count, and issues.
- **Sims3PackXmlMetadata**: Record containing parsed Sims3Pack XML manifest fields (`RootElementName`, `DeclaredEncoding`, `RawXmlSizeBytes`, `EmbeddedFileCount`, `EmbeddedFileNames`, `Title?`, `AssetId?`, `AssetType?`, `Description?`).
- **Sims3PackParseResult**: Immutable record containing Sims3Pack XML parsing operation status (`IsSuccess`, `Sims3PackXmlMetadata?`, `Issues`).
- **Sims3PackCatalogEntry**: Immutable record representing a scanned embedded candidate payload (`EntryIndex`, `Kind`, `DataOffset`, `EstimatedSizeBytes?`, `DisplayName?`, `Issues`).
- **Sims3PackCatalogResult**: Immutable record containing catalog scan operation status (`IsSuccess`, `Entries`, `Issues`).
- **Sims3PackPayloadExportRequest**: Value record specifying source `.sims3pack` path, `Sims3PackCatalogEntry`, target `.package` path, and `AllowOverwrite` flag.
- **Sims3PackPayloadExportResult**: Value record specifying payload export operation status (`IsSuccess`), source path, target path, exported byte count, and issues.

### 2. SimsConverter.Application
Contains application use-case orchestration services, contracts, and UI-independent presentation models.

#### Key Entities & Services
- **IPackageInspectionService**: Application use-case contract for package resource inspection (`InspectFileAsync`).
- **PackageInspectionService**: Use-case orchestration service translating binary `DbpfParseResult` into presentation-ready `PackageInspectionResult`.
- **IResourceExportService**: Application contract for single raw resource export (`ExportResourceAsync`).
- **ResourceExportService**: Use-case service generating sanitized deterministic output filenames (`00B2D882_00000000_123456789ABCDEF0.raw`) and delegating export to Package layer.
- **SingleResourceExportRequest**: Application request model combining source path, selected `PackageResourceRow`, destination directory, and overwrite flag.
- **PackageInspectionResult**: Presentation result record containing status (`IsSuccess`), file path, header summary, resource rows, and issues.
- **PackageResourceRow**: Presentation-ready row record exposing hex-formatted keys (`TypeHex`, `GroupHex`, `InstanceHex`, `FormattedKey`), offsets, sizes, and compression names.
- **ISims3PackInspectionService**: Application use-case contract for Sims3Pack inspection and embedded payload export (`InspectFileAsync`, `ExportPayloadAsync`).
- **Sims3PackInspectionService**: Orchestration service unifying detection, XML parsing, catalog scanning, and payload export into UI-ready presentation results.
- **Sims3PackPayloadRow**: Presentation-ready row model (`EntryIndex`, `Kind`, `DataOffsetHex`, `EstimatedSizeFormatted`, `DisplayName`, `CanExport`, `Issues`).
- **Sims3PackInspectionResult**: Presentation result record containing status (`IsSuccess`), file path, XML metadata fields, `PayloadRows`, and aggregated `Issues`.
- **Sims3PackExportRequest**: Application export request model (`SourceSims3PackPath`, `SelectedRow`, `OutputDirectory`, `CustomFileName`, `AllowOverwrite`).
- **Sims3PackExportResult**: Application export result record containing status (`IsSuccess`), file paths, exported byte count, and `Issues`.

### 3. SimsConverter.Package
Contains contracts and data structures for DBPF, package container inspection, Sims3Pack container detection, XML manifest parsing, catalog scanning, and safe raw binary byte range extraction.

#### Key Entities & Contracts
- **PackageHeaderSummary**: Record summarizing index count, DBPF versioning, and detected game version.
- **IPackageInspector**: Interface for package header inspection.
- **IPackageDetector**: Primary interface for safe, bounds-checked container inspection (`Detect`, `DetectAsync`, `DetectFileAsync`).
- **PackageDetector**: Safe implementation executing non-destructive DBPF container detection and delegating `.sims3pack` files to `ISims3PackDetector`.
- **ISims3PackDetector**: Dedicated contract for `.sims3pack` binary header detection (`DetectFileAsync`, `DetectAsync`).
- **Sims3PackDetector**: Implementation executing binary TS3Pack Little-Endian header detection (`signatureLength`, `"TS3Pack"`, `version`, `xmlLength`).
- **ISims3PackXmlParser**: Primary contract for parsing Sims3Pack XML metadata sections safely (`ParseFileAsync`, `ParseAsync`).
- **Sims3PackXmlParser**: Implementation executing exact `xmlLength` byte reads and secure XML parsing (XXE/DTD disabled, 10MB allocation limit).
- **ISims3PackPayloadCatalogScanner**: Primary contract for scanning Sims3Pack archive payloads (`ScanFileAsync`, `ScanAsync`).
- **Sims3PackPayloadCatalogScanner**: Non-destructive implementation scanning archive sections for DBPF (`"DBPF"`) and PNG magic signatures, ordering entries by `DataOffset`.
- **ISims3PackPayloadExporter**: Primary contract for exporting embedded DBPF payload candidates from Sims3Pack containers (`ExportAsync`).
- **Sims3PackPayloadExporter**: Safe implementation executing bounds-checked DBPF byte range extraction, canonical path check (`S3PE008`), DBPF magic verification (`S3PE007`), and atomic temp file creation.
- **IDbpfPackageParser**: Primary contract for parsing DBPF container headers and resource index entry tables safely (`Parse`, `ParseAsync`, `ParseFileAsync`).
- **DbpfPackageParser**: Robust parser implementation performing bounds checking, Little-Endian reading, and safe index extraction.
- **IPackageResourceExporter**: Primary contract for raw binary byte range export (`ExportAsync`).
- **PackageResourceExporter**: Implementation executing read-only byte range extraction, bounds checking, overwrite protection, canonical path check (`EXPE008`), and atomic temp file creation.

### 4. SimsConverter.App
Avalonia UI application root containing composition root, dependency injection, and MVVM presentation ViewModels.

#### Key Components
- **App Composition Root**: Wires `IDbpfPackageParser`, `IPackageInspectionService`, `IPackageResourceExporter`, `IResourceExportService`, `ISims3PackDetector`, `ISims3PackXmlParser`, `ISims3PackPayloadCatalogScanner`, `ISims3PackPayloadExporter`, `ISims3PackInspectionService`, `IPackageDetector`, `IFilePickerService`, and `ResourceInspectorViewModel` via `Microsoft.Extensions.DependencyInjection`.
- **ResourceInspectorViewModel**: Reactive ViewModel (`CommunityToolkit.Mvvm`) managing `SelectedFilePath`, `SelectedResource`, `SelectedSims3PackPayload`, `IsSims3PackMode`, `IsBusy`, `StatusMessage`, `Resources`, `Sims3PackPayloads`, `Issues`, `CanInspect`, `CanExport`, `CanExportSims3PackPayload`, `InspectCommand`, `ExportResourceCommand`, and `ExportSims3PackPayloadCommand`.
- **MainWindow.axaml**: Semantic XAML view bound to `ResourceInspectorViewModel` with monospaced hex DataGrid columns, Sims3Pack metadata header, payload DataGrid, issue diagnostic border, and action buttons.

---

## Strategic Design Rules
1. `SimsConverter.Domain` MUST NOT depend on any external libraries or other projects in the solution.
2. Domain entities must remain immutable or encapsulate state changes strictly.
3. `SimsConverter.Application` MUST NOT reference Avalonia UI or presentation assemblies (`SimsConverter.App`), and MUST NOT perform low-level binary byte parsing.
4. `SimsConverter.App` MUST NOT perform binary parsing or directly reference `System.Buffers.Binary.BinaryPrimitives`, `System.Xml`, or low-level file streams. All inspection, XML parsing, catalog scanning, and export operations MUST execute via application/package services.
5. **Prohibition of Magic-Only Guessing**: Detecting DBPF container magic alone MUST NOT mark the game version as `Sims3` or `Sims4`. Game version MUST remain `GameVersion.Unknown` until verified by game-specific resource signatures.
6. **Sims3Pack Container Policy**: Sims3Pack XML manifest parsing MUST enforce secure settings (`DtdProcessing = Prohibit`, `XmlResolver = null`) to prevent XXE and DTD expansion attacks, and MUST read exactly `xmlLength` bytes starting from calculated `headerLength`.
7. **Sims3Pack Archive Catalog Policy**: Archive scanning MUST be read-only (`FileShare.Read`) and non-destructive. Payload candidates MUST be detected by magic signature (`"DBPF"`, PNG magic) and returned in deterministic order by `DataOffset`.
8. **Sims3Pack Embedded Payload Export Policy**: Embedded DBPF payload export MUST verify candidate kind (`DbpfPackage`), canonical path separation (`S3PE008`), post-export DBPF magic header verification (`S3PE007`), and atomic temp file creation.
9. **Sims3Pack Application Boundary Policy**: Application inspection services MUST aggregate diagnostic issues across detection, XML parsing, and catalog scanning, and MUST project domain catalog entries into presentation-ready `Sims3PackPayloadRow` models for UI consumption.
10. **Non-Destructive File Inspection & Export**: Source package files MUST be accessed read-only (`FileShare.Read`) and MUST NEVER be modified or overwritten during detection, inspection, XML parsing, catalog scanning, or export workflows.
11. **Atomic Export Writing**: Raw resource export MUST write bytes to a temporary file (`.tmp.guid`) first and atomically move to the target path upon complete validation.
12. **Strict Bounds Checking**: Binary parsing and byte range export operations MUST perform explicit length and bounds verification before accessing offsets, producing diagnostic issues rather than uncaught exceptions.
