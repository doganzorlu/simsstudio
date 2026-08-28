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
- **TextureClassificationKind**: Enum representing texture resource classification (`Unknown`, `TextureCandidate`, `KnownTexture`, `UnsupportedTexture`).
- **TextureMapKind**: Enum representing texture map role (`Unknown`, `Diffuse`, `Normal`, `Specular`, `Alpha`, `Mask`, `Icon`, `Thumbnail`).
- **DdsTextureFormatKind**: Enum representing DDS format family (`Unknown`, `Dxt1`, `Dxt3`, `Dxt5`, `Ati1`, `Ati2`, `UncompressedRgba`).
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
- **TextureResourceClassification**: Immutable record representing texture classification outcome (`PackageResourceId ResourceId`, `TextureClassificationKind Classification`, `TextureMapKind MapKind`, `string FormatName`, `GameVersion DetectedGameVersion`, `IReadOnlyList<ConversionIssue> Issues`).
- **TextureResourceExtractRequest**: Value record specifying source package path, `PackageResourceEntry`, `TextureResourceClassification`, output directory, custom filename, and `AllowOverwrite` flag.
- **TextureResourceExtractResult**: Value record specifying extraction status (`IsSuccess`), source path, output path, exported byte count, and issues.
- **DdsTextureMetadata**: Immutable record containing DDS header layout metadata (`Width`, `Height`, `MipMapCount`, `DdsTextureFormatKind FormatKind`, `FourCC`, `PixelFormatFlags`, `RgbBitCount`, `Caps`).
- **DdsParseResult**: Immutable record specifying DDS header parsing status (`IsSuccess`, `DdsTextureMetadata?`, `Issues`).
- **DdsPayloadValidationResult**: Immutable record specifying DDS payload validation status (`IsSuccess`, `ExpectedPayloadBytes`, `ActualPayloadBytes`, `Issues`).

### 2. SimsConverter.Textures
Contains texture resource type constants, candidate classification services, deterministic extension mapping, raw texture extraction boundaries, bounds-checked DDS binary header parsing, and overflow-safe payload size validation.

#### Key Entities & Services
- **TextureTypeIds**: Static class defining verified TS3 and TS4 texture TypeIds (`Ts3DdsTexture`, `Ts3SnapshotThumbnail`, `Ts4Rle2Texture` `0x3453CF95`, `Ts4LrleTexture` `0x2BC04EDF`, `Ts4PngImage` `0x2F7D0004`, `Ts4CasPartThumbnail` `0x3C1AF1F2`).
- **ITextureResourceClassifier**: Primary interface for texture candidate classification (`Classify`, `ClassifyBatch`).
- **TextureResourceClassifier**: Safe implementation performing deterministic TypeId classification, game version matching, map role assignment, and diagnostic issue reporting for unrecognized types (`TEXC001`).
- **ITextureResourceExtractor**: Primary interface for texture raw payload extraction (`ExtractAsync`).
- **TextureResourceExtractor**: Safe implementation verifying texture classification (`KnownTexture`), mapping extensions (.dds, .png, .rle2, .lrle), checking canonical path separation (`TEXE008`), and delegating byte range export to `IPackageResourceExporter`.
- **IDdsHeaderParser**: Primary interface for bounds-checked DDS binary header parsing (`Parse`).
- **DdsHeaderParser**: Safe implementation parsing DDS magic (`"DDS "`), header size (128 bytes), dimensions, FourCC string, format family (`DdsTextureFormatKind`), and preserving stream invariants.
- **IDdsPayloadValidator**: Primary interface for DDS payload size estimation and truncation validation (`Validate`).
- **DdsPayloadValidator**: Safe implementation calculating exact block-compressed (8B/16B per 4x4 block) and uncompressed mipmap chain payload sizes using `checked` overflow-safe arithmetic.

### 3. SimsConverter.Application
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
- **ITextureInspectionService**: Application use-case contract for texture resource inspection and UI row projection (`InspectPackageTexturesAsync`, `InspectPackageTextures`).
- **TextureInspectionService**: Application use-case service translating package resources into presentation-ready `TextureResourceRow` models using `ITextureResourceClassifier`.
- **TextureInspectionRequest**: Application request model (`PackageFilePath`, `GameVersionHint`).
- **TextureResourceRow**: Presentation-ready row record exposing formatted hex keys, format names, map kinds, game versions, capability flags (`CanExtractRawPayload`, `CanParseDdsHeader`), and issues.
- **TextureInspectionResult**: Presentation result record containing status (`IsSuccess`), file path, `Rows`, and aggregated `Issues`.

### 4. SimsConverter.Package
Contains contracts and data structures for DBPF, package container inspection, Sims3Pack container detection, XML manifest parsing, catalog scanning, safe raw binary byte range extraction, and real fixture validation.

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
- **Sims3PackRealFixtureValidationTests**: Test harness for validating real-world `.sims3pack` files in `fixtures/local/` and generating compatibility reports without committing binary game files.
- **IDbpfPackageParser**: Primary contract for parsing DBPF container headers and resource index entry tables safely (`Parse`, `ParseAsync`, `ParseFileAsync`).
- **DbpfPackageParser**: Robust parser implementation performing bounds checking, Little-Endian reading, and safe index extraction.
- **IPackageResourceExporter**: Primary contract for raw binary byte range export (`ExportAsync`).
- **PackageResourceExporter**: Implementation executing read-only byte range extraction, bounds checking, overwrite protection, canonical path check (`EXPE008`), and atomic temp file creation.

### 5. SimsConverter.App
Avalonia UI application root containing composition root, dependency injection, and MVVM presentation ViewModels.

#### Key Components
- **App Composition Root**: Wires `IDbpfPackageParser`, `IPackageInspectionService`, `IPackageResourceExporter`, `IResourceExportService`, `ISims3PackDetector`, `ISims3PackXmlParser`, `ISims3PackPayloadCatalogScanner`, `ISims3PackPayloadExporter`, `ISims3PackInspectionService`, `IPackageDetector`, `IFilePickerService`, `ITextureResourceClassifier`, `ITextureResourceExtractor`, `IDdsHeaderParser`, `IDdsPayloadValidator`, `ITextureInspectionService`, and `ResourceInspectorViewModel` via `Microsoft.Extensions.DependencyInjection`.
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
10. **Real Fixture Validation Policy**: Real-world `.sims3pack` files MUST be stored in `fixtures/local/` and ignored by `.gitignore`. Validation tests MUST execute gracefully in a skipped state when no local fixtures are present.
11. **Texture Classification Policy**: Texture candidate classification MUST be deterministic based on verified TypeId constants (`0x3453CF95` [RLE2], `0x2BC04EDF` [LRLE], `0x2F7D0004` [PNG Image], `0x3C1AF1F2` [CAS Part Thumbnail]). Unverified TypeIds MUST NOT be added to the catalog. Unambiguous engine-specific TypeIds map automatically to `GameVersion.Sims4`; ambiguous TypeIds (`0x00B2D882`) retain `GameVersion.Unknown` unless an explicit game version hint is provided. Unrecognized TypeIds MUST produce diagnostic warning issues (`TEXC001`).
12. **Texture Payload Extraction Policy**: Texture payload extraction MUST verify that `Classification == KnownTexture` before exporting (`TEXE001`). `Classification.ResourceId` MUST match `Entry.Id` (`TEXE004`). Raw byte range copying MUST delegate to `IPackageResourceExporter`. Extensions MUST map deterministically (.dds, .png, .rle2, .lrle). Canonical path separation MUST be enforced (`TEXE008`).
13. **DDS Header Parsing Policy**: DDS binary header parsing MUST enforce minimum 128-byte size (`TEXD001`), `"DDS "` magic signature (`TEXD002`), positive dimensions (`TEXD003`), struct sizes (`TEXD005`, `TEXD006`), format family classification (`DXT1`, `DXT3`, `DXT5`, `ATI1`, `ATI2`, `UncompressedRgba`), and stream position invariants. Unrecognized FourCC values MUST produce diagnostic warning issues (`TEXD004`). Zero pixel decoding or image rendering is permitted in the header parser.
14. **DDS Payload Validation Policy**: DDS payload size estimation MUST calculate full mipmap chain level byte counts using exact block formulas ($4 \times 4$ blocks, 8B for DXT1/ATI1, 16B for DXT3/DXT5/ATI2) and pitch calculations for uncompressed RGBA. MipMapCount MUST NOT exceed the maximum valid mip count ($\lfloor \log_2(\max(W, H)) \rfloor + 1$), returning controlled error `TEXV005` if exceeded. Arithmetic calculations MUST use `checked` blocks to detect overflow (`TEXV003`). Truncated payloads MUST fail validation with error `TEXV001`. Unknown formats MUST return `IsSuccess = false` with diagnostic warning `TEXV004`.
15. **Texture Inspection Application Boundary Policy**: `TextureInspectionService` MUST project package entries into presentation-ready `TextureResourceRow` models using `ITextureResourceClassifier`. Capability flags (`CanExtractRawPayload`, `CanParseDdsHeader`) MUST be set deterministically based on classification outcome. Unknown resources MUST be included with `ClassificationKind = Unknown` and diagnostic warning `TEXC001` (NEVER silently omitted). Zero low-level binary byte parsing or file stream reads are permitted in the Application layer.
16. **Non-Destructive File Inspection & Export**: Source package files MUST be accessed read-only (`FileShare.Read`) and MUST NEVER be modified or overwritten during detection, inspection, XML parsing, catalog scanning, or export workflows.
17. **Atomic Export Writing**: Raw resource export MUST write bytes to a temporary file (`.tmp.guid`) first and atomically move to the target path upon complete validation.
18. **Strict Bounds Checking**: Binary parsing and byte range export operations MUST perform explicit length and bounds verification before accessing offsets, producing diagnostic issues rather than uncaught exceptions.
