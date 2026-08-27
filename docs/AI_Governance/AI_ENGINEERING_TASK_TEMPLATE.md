# AI Engineering Task Template

This document defines the standard format used when **AI1 (Product Governance Agent)** creates engineering tasks for **AI2 (Engineering Executor)**.

The purpose of this template is to ensure that engineering instructions are:

* clear
* minimal
* implementable
* architecture compliant

AI1 must always use this structure when instructing AI2.

---

# Task Metadata

Task ID:

Title:

Requested By:

Related Feature:

Priority:

---

# Feature Summary

Provide a short explanation of the feature or capability.

Explain:

* what problem is being solved
* which module/domain is affected
* why the feature is needed

Keep this section concise.

---

# Architecture Context

Explain where the change fits within the system architecture.

Describe:

* related modules
* affected services
* affected domain models

AI2 must understand the architectural boundary before implementation.

---

# Domain Entities

List any required domain entities or modifications.

Example:

* Risk
* Audit
* AuditFinding
* CorrectiveAction

If an entity already exists, specify how it should be extended.

---

# Workflow Definition

Describe the business workflow involved in the feature.

Example:

Risk
→ Risk Review
→ Audit Planning
→ Audit Execution
→ Finding
→ Corrective Action

AI2 must implement the workflow according to this flow.

---

# Integration Points

Identify which existing modules must integrate with this feature.

Example:

* Risk module
* Corrective Action module
* Action Tracking module

Explain how they interact.

---

# Implementation Rules

Define constraints AI2 must follow.

Examples:

* Do not introduce new modules unnecessarily
* Prefer extending existing domain models
* Implement minimal viable functionality first
* Maintain loose coupling between modules
* Separate menu visibility from CRUD permissions
* Define row-level boundary explicitly when auth changes

---

# Engineering Tasks

Break the work into small independent tasks.

Example:

Task 1: Create new domain entity
Task 2: Implement service logic
Task 3: Add API endpoint
Task 4: Integrate with existing module
Task 5: Add migration if required

Tasks should be incremental.

---

# Acceptance Criteria

Define when the task is considered complete.

Examples:

* Feature works according to workflow
* No architectural rule violations
* No duplicate modules created
* Existing modules remain compatible

---

# Testing Notes

Describe basic validation steps.

Example:

* Create test entity
* Execute workflow
* Validate integration with related modules

AI2 should provide simple verification instructions.

---

## UI Form / List Policy Compliance

If the task includes form pages or list screens, AI1 must confirm:

* Form dropdown/combobox bileşenleri server-side search (`q`, debounce, minChars, race-safe) kullanır
* `api.*.list({})` anti-pattern yok
* `limit: 500` kullanımı varsa `// DROPDOWN-POLICY-EXCEPTION:` yorumuyla belgelenmiş
* Dinamik listeler `SearchableSelect` veya `FilteredCheckboxList` kullanır
* Loading / empty / error state'leri ele alınmış
* Backend endpoint `q` parametresi destekliyor
* Hiyerarşik ağaç / klasör istisnalar `// DROPDOWN-POLICY:` yorumuyla belgelenmiş

See: `DROPDOWN_DYNAMIC_LOADING_POLICY.md`

---

# SDLC Compliance Notes

AI1 should confirm:

* the feature follows architecture rules
* no unnecessary complexity is introduced
* module boundaries remain respected
* mandatory pre-push quality gate policy is included (clean `dotnet build` and `dotnet test` if applicable)
* UI form/list dropdown policy is satisfied (see above)
* authorization changes follow `docs/security/APPLICATION_SECURITY_ARCHITECTURE.md`
* permission matrix is required when auth behavior changes

This section acts as the governance checkpoint.

---

# Expected Output from AI2

When AI2 completes the task it must provide:

* Implementation summary
* List of modified files
* New entities or models
* Database migrations (if any)
* Testing notes
* Quality gate evidence (build/test verification command and result)

---

# Key Principle

AI1 defines **what must be built**.

AI2 decides **how it is implemented in code**, within the architectural constraints.
