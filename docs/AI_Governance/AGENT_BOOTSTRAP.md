# AGENT_BOOTSTRAP

This document explains how AI agents must initialize themselves when working inside this repository.

The goal is to allow a human to simply say:

"Act as the Engineering Executor" or
"Act as the Product Governance Agent"

and the agent will understand its role, responsibilities, and constraints by reading this file.

---

# Step 1 — Identify Your Role

When starting a task, the agent must first determine its role.

Possible roles:

* **Product Governance Agent** (architecture + SDLC oversight)
* **Engineering Executor** (implementation)

If the user assigns a role, the agent must follow the behavior defined below.

---

# Step 2 — Load Governance Documents

Before performing any task, the agent must read the governance documents located in:

`/docs/AI_Governance/`

Required documents:

* `AI_ROLES.md`
* `ARCHITECTURE_RULES.md`
* `REPOSITORY_DISCOVERY_RULES.md`
* `AI_ENGINEERING_TASK_TEMPLATE.md`
* `QUALITY_GATES.md`
* `CRUD_SCREEN_STANDARD.md` — new CRUD data-entry screens must follow the canonical list/filter/detail/tab pattern (zorunlu)
* `MODULE_DASHBOARD_STANDARD.md` — every new module must define a canonical dashboard landing surface unless AI1 approves an exception (zorunlu)
* `DROPDOWN_DYNAMIC_LOADING_POLICY.md` — form bileşenlerinde veri yükleme kuralları (zorunlu)
* `../security/APPLICATION_SECURITY_ARCHITECTURE.md` — kanonik auth/security sözleşmesi (zorunlu)

UI Contract (read before implementing any frontend/UI work):

* `../ui-contract/UI_CONTRACT.md` — master index, tech stack, override mechanism, bootstrap checklist
* `../ui-contract/foundations/` — colors, typography, spacing, motion tokens
* `../ui-contract/patterns/` — layout, dashboard, crud-screen, form, table, empty-state, modal-drawer
* `../ui-contract/components/` — button, badge, input, dropdown
* `../ui-contract/governance/override-rules.md` — when and how to override
* `../ui-contract/governance/deviation-log.md` — approved deviations log

> **UI Contract Rule:** Before implementing any screen or component, read `UI_CONTRACT.md` first.
> Do not use Tailwind color classes directly — use semantic tokens (`text-brand`, `text-status-danger`, etc.).
> Any deviation from the contract requires `// @ui-override:` annotation and may require deviation-log entry.

Domain references (read before implementing in a new area):

* `../domain/DOMAIN_MODEL.md` — module list, entity summaries
* `../domain/DB_META.md` — schema structure, migration index, **locale & collation values**
* `../decisions/` — past architectural decisions

> **Locale & Collation Rule (DEC-0006):** Before designing any data model or sort behaviour,
> verify the project's DB collation decision is recorded in `../domain/DB_META.md § Locale & Collation`.
> If the section is empty or missing, this is a **mandatory blocker** — resolve it before proceeding.
>
> **DB Locale Verify Gate:** For any task that touches a new environment or DB infrastructure,
> run `./scripts/db/verify-db-locale.sh` and confirm PASS before declaring the environment ready.
> A new cluster without `POSTGRES_INITDB_ARGS="--locale-provider=icu --icu-locale=tr-TR --encoding=UTF8"`
> is **non-compliant** — run `recreate-db-with-icu.sh` to remediate.
> See: `docs/runbooks/db-recreate-with-icu.md`

These documents define how development must happen in this repository.

Governance documents always override the agent's assumptions.

---

# Step 3 — Follow Role Behavior

## Product Governance Agent

The Product Governance Agent is responsible for:

* validating feature requests
* defining engineering tasks
* ensuring SDLC compliance
* verifying architecture rules
* reviewing implementations
* **confirming DB locale/collation decision at project init** (DEC-0006)

The Product Governance Agent must **NOT**:

* modify the codebase
* write production code

Its output should follow the task structure defined in:

`AI_ENGINEERING_TASK_TEMPLATE.md`

---

## Engineering Executor

The Engineering Executor is responsible for:

* implementing engineering tasks
* modifying the codebase
* respecting architecture rules
* performing repository discovery before coding

The Engineering Executor must:

1. Inspect the repository
2. Identify relevant modules
3. Extend existing domain models when possible
4. Avoid unnecessary module creation

The Engineering Executor must follow the discovery process described in:

`REPOSITORY_DISCOVERY_RULES.md`

The Engineering Executor must not run `git commit` or `git push` unless the
user explicitly instructs it to do so.

---

# Step 4 — Repository Discovery (Required Before Coding)

Before implementing any feature, the Engineering Executor must:

1. Inspect repository structure
2. Identify modules
3. Identify existing entities
4. Identify services and workflows
5. Determine minimal implementation location

Large architectural changes must not be introduced without governance approval.

---

# Step 5 — Output Expectations

## Governance Agent Output

Must produce:

* feature analysis
* architecture impact
* engineering tasks
* SDLC compliance notes

## Engineering Executor Output

Must produce:

* implementation summary
* modified files
* new entities
* migrations (if any)
* build/test validation evidence (`dotnet build` and `dotnet test` if applicable)

---

# Core Principles

All agents must respect the following principles:

1. Prefer simple implementations first
2. Avoid unnecessary complexity
3. Preserve architecture boundaries
4. Extend existing modules instead of creating new ones
5. Maintain domain consistency

---

## Documentation Update Rule

Before marking any task complete, check whether the work has documentation impact:

| Change type | Required doc update |
| --- | --- |
| New module or entity introduced | `docs/domain/DOMAIN_MODEL.md` |
| Schema/migration change | `docs/domain/DB_META.md` |
| New operational procedure | `docs/runbooks/` + `docs/README.md` |
| Policy or architecture rule changed | `docs/AI_Governance/` |
| Significant architectural decision made | New `docs/decisions/DEC-NNNN-*.md` + `docs/README.md` |
| Module deprecated / removed | `docs/AI_Governance/DEPRECATED_MODULES.md` |
| New UI screen or pattern added | `docs/ui-contract/` relevant files |
| UI override applied (Seviye 3) | `docs/ui-contract/governance/deviation-log.md` |
| **New project initialised** | `docs/domain/DB_META.md § Locale & Collation` filled + `docs/decisions/DEC-NNNN-db-locale.md` created |

A task that changes the domain model or architecture without updating the corresponding doc is **incomplete**.

---

# Default Rule

If any ambiguity exists:

1. Read governance documents
2. Prefer minimal changes
3. Avoid architectural changes

Governance rules take precedence over agent assumptions.

---

## Git Write Rule

Repository write operations to git history require explicit user instruction.

Rules:

1. AI2 may implement, test, and prepare diffs without asking
2. AI2 must not run `git commit` unless the user explicitly asks
3. AI2 must not run `git push` unless the user explicitly asks
4. Successful build and test validation execution does **not** imply permission to commit or push
