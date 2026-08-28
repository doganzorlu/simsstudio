# DEC-0010: Sims3Pack XML Metadata Section Parser Architecture

- **Status**: Pending Approval
- **Date**: 2026-08-28
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-S3P-002-R1

## Context
SIMSStudio requires a secure, bounded parser to extract XML metadata section manifests from `.sims3pack` outer container files after verifying the binary `TS3Pack` header, returning structural, observable presentation models without memory risk or unverified semantic mapping assumptions.

## Decision

1. **Bounded Allocation Guard (`MaxXmlMetadataBytes` = 10MB)**:
   - Defined `Sims3PackXmlParser.MaxXmlMetadataBytes = 10 * 1024 * 1024` (10 MB upper limit).
   - Before allocating memory for the XML buffer (`new byte[xmlLength]`), `Sims3PackXmlParser` checks if `xmlLength > MaxXmlMetadataBytes`.
   - If `xmlLength` exceeds 10MB, the parser immediately returns controlled failure `S3PX006` without attempting memory allocation. This guard protects both seekable and non-seekable streams against denial-of-service or `OutOfMemoryException` risks.

2. **Strict Signature Verification**:
   - Signature MUST strictly equal `"TS3Pack"` (7 bytes ASCII) or `"TS3Pack\0"` (8 bytes ASCII with null terminator).
   - Space-padded or altered signatures (e.g. `"TS3Pack  "`) are strictly rejected with controlled error `S3PX005`.

3. **Structural Container Metadata Contract**:
   - `Sims3PackXmlMetadata` record exposes structural, observable container fields:
     - `RootElementName`: Local name of XML root element (e.g. `"Sims3Pack"`)
     - `DeclaredEncoding`: Encoding declared in `<?xml encoding="..."?>` (e.g. `"utf-8"`)
     - `RawXmlSizeBytes`: Exact byte size of XML section (`(long)xmlLength`)
     - `EmbeddedFileCount`: Count of embedded package files
     - `EmbeddedFileNames`: Immutable list of embedded package filenames
     - Best-effort optional semantic fields (`Title?`, `AssetId?`, `AssetType?`, `Description?`)

4. **Exact `xmlLength` Bounds Read & Boundary Protection**:
   - Reads EXACTLY `xmlLength` bytes starting from the calculated `headerLength` offset (`4 + signatureLength + 2 + 4`). Extra trailing stream bytes (embedded DBPF packages or PNG thumbnails) are NOT read into the XML buffer or touched by the XML parser.

5. **Strict XML Security Settings (XXE & DTD Protection)**:
   - Configures `XmlReaderSettings`:
     - `DtdProcessing = DtdProcessing.Prohibit` (DTD processing disabled)
     - `XmlResolver = null` (External entity resolution prohibited)
     - `MaxCharactersFromEntities = 1024`
   - Prevents XXE (XML External Entity) expansion attacks and DTD-based denial-of-service vulnerabilities.

## Consequences
- Guarantees zero memory allocation risk on malicious headers or non-seekable streams.
- Provides clean, structural container metadata aligned with task acceptance criteria.
- Protects the application against malicious XML payloads (XXE, DTD bombs, truncated buffers).
- Establishes a solid foundation for upcoming task `SIMS-S3P-003` (embedded DBPF payload extraction).
