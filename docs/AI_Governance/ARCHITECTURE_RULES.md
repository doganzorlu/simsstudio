# Architectural Rules & Principles

1. **Strict Layer Separation**:
   - `App`: UI components, ViewModels, Composition Root. Zero binary parsing.
   - `Application`: Orchestration, application services, contracts.
   - `Domain`: Enterprise business rules, domain entities, enums. Zero external dependencies.
   - `Infrastructure`: I/O, file system, OS wrappers, external adapters.
   - `Package`: Low-level DBPF container parsing and package layout inspectors.

2. **Dependency Direction**:
   - Dependencies must point inwards towards Domain.
   - Circular dependencies between layers are strictly prohibited.

3. **Immutability & Safety**:
   - Prefer record types and immutable data structures for domain events, reports, and issues.
   - Explicit nullable reference types must be enabled (`<Nullable>enable</Nullable>`).
