# Database Meta & Infrastructure Configuration

## Status
**No Database (RDBMS/NoSQL) is currently used by SIMSStudio.**

SIMSStudio is a desktop file-processing and conversion tool operating directly on local files (DBPF packages, 3D assets, and textures).

## Locale & Collation Policy (DEC-0006 Compliance)
- **Database Engine**: None
- **Locale & Collation**: Not applicable (N/A). String comparisons and sorting within domain logic MUST use standard `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` unless locale-sensitive sorting is explicitly required.

If database infrastructure is added in future iterations, standard ICU collation policies (`tr-TR` / UTF-8) and `verify-db-locale.sh` execution will be mandatory.
