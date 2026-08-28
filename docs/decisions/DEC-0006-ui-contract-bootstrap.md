# DEC-0006: UI Contract Bootstrap for Avalonia Desktop

- **Status**: Decision Drafted / Pending Approval
- **Date**: 2026-08-27
- **Author**: AI2 (Engineering Executor)
- **Task**: SIMS-GOV-001

## Context
As SIMSStudio introduces Avalonia UI desktop presentation surfaces (`SimsConverter.App`), a comprehensive UI contract governance suite (`docs/ui-contract/`) is required to establish design consistency, typography scales, layout patterns, component standards, and architectural guardrails before production UI screens grow in complexity.

## Decision

1. **Governance Directory Structure**:
   Established `docs/ui-contract/` containing master `UI_CONTRACT.md`, foundations (`colors.md`, `typography.md`, `spacing.md`, `motion.md`), patterns (`layout.md`, `dashboard.md`, `crud-screen.md`, `form.md`, `table.md`, `empty-state.md`, `modal-drawer.md`), components (`button.md`, `badge.md`, `input.md`, `dropdown.md`), and governance (`override-rules.md`, `deviation-log.md`).

2. **Core Presentation Guardrails**:
   - **Zero Binary Parsing in UI**: Presentation components (`SimsConverter.App`) MUST NOT execute low-level binary parsing or byte offset manipulation (`System.Buffers.Binary`). Inspection and parsing logic must be executed via `IPackageInspectionService`.
   - **Transparent Diagnostic Reporting**: Presentation elements MUST report diagnostic issues (`ConversionIssue`) returned by lower layers without swallowing exceptions or masking corrupt container states.
   - **Semantic Token Usage**: Layout elements MUST bind to theme resources rather than hardcoded inline hex colors or arbitrary static offsets.

3. **Desktop Adaptation**:
   - Adapted design patterns to **Avalonia UI 12 Desktop** (`FluentTheme`, `ThemeResource`, desktop density font scales, 8px layout grid).

## Consequences
- Guarantees visual and architectural consistency across future UI feature tasks.
- Prevents UI layer from leaking low-level binary byte parsing.
