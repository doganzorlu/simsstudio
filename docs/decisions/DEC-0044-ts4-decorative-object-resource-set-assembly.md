# DEC-0044: TS4 Decorative Object Resource Set Assembly

- **Status:** Approved
- **Date:** 2026-09-07
- **Author:** SIMSStudio Core Team

---

## Context and Problem Statement

Following the establishment of the TS4 target package writer boundary (SIMS-CONV-007), the pipeline must assemble the complete TS4 decorative object resource set. Previously, the package write plan only contained mesh payloads.

To produce a full TS4 decorative object package output:
1. All conversion components—canonical meshes, DDS/RLE textures, RIG (`0x8EAF13DE`), RSLT (`0xD3044521`), and model metadata—must be aggregated into a single, unified output set.
2. Resource TypeIds and TGIs must be deterministically preserved and sorted by `FormattedKey` (`StringComparison.Ordinal`).
3. Texture payload bytes must be extracted using verified `PackageResourceEntry` metadata.
4. Mesh-material-texture bindings must rely strictly on verified references (`ResourceLinks`).
5. The complete resource set index and payload boundaries must be verified against DBPF 2.0 specs.

---

## Solution Design

1. **Model Hardening**:
   - `DecorativeObjectSourceTextureAsset`: Extended with `PackageResourceEntry? Entry` and `IReadOnlyList<byte>? RawPayload`.
   - `DecorativeObjectResourceSetReport`: Detailed assembly summary reporting resource counts, category breakdown (mesh, texture, RIG, RSLT, other), total payload size, and verified reference link counts.
2. **Bundle Builder Hardening**:
   - Reads raw payload bytes for texture candidates using `IPackageResourcePayloadReader`.
3. **Plan Assembly Builder (`DecorativeObjectPackageWritePlanBuilder`)**:
   - Aggregates mesh bundles, texture assets, RIG resources, and RSLT resources.
   - Extracts payloads for RIG and RSLT resources from source package using `IPackageResourcePayloadReader`.
   - Sorts all planned output entries deterministically by `FormattedKey` (`StringComparison.Ordinal`).
   - Generates `DecorativeObjectResourceSetReport`.

---

## Quality Gates

- Full DBPF 2.0 container index validation (`DbpfPackageParser.Parse`).
- Zero speculative mesh-texture links.
- 0 build errors, 0 build warnings.
- 100% test pass rate across unit and real fixture test suites.
