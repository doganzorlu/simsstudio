# Repository Discovery Rules

This document defines how AI agents must analyze and understand the repository before implementing any change.

The goal is to prevent:

* duplicate modules
* duplicate domain models
* unnecessary architectural complexity

AI agents must inspect the repository structure before proposing or implementing changes.

---

# Discovery Process

Before implementing any feature, the AI agent must perform the following steps.

1. Inspect the repository structure.
2. Identify existing modules.
3. Locate relevant domain models.
4. Check existing services and workflows.
5. Verify whether the requested capability already exists.

Only after this analysis should implementation planning begin.

---

# Module Discovery

AI agents must determine:

* which module owns the domain
* which module contains related logic

If a suitable module already exists, the feature must be implemented there.

Creating a new module must be considered a last resort.

---

# Entity Discovery

Before creating a new entity, AI agents must check whether a similar entity already exists.

If a related entity exists, prefer:

* extending the entity
* adding new fields
* adding services around the entity

Avoid creating semantically duplicated models.

---

# Service Discovery

Before creating new services or workflows, AI agents must verify whether the behavior already exists.

If an existing service provides most of the functionality, extend it rather than replacing it.

---

# File Modification Strategy

When implementing a feature, AI agents should:

* modify the smallest number of files necessary
* avoid unnecessary refactors
* preserve existing structure

Incremental changes are preferred over large modifications.

---

# Dependency Awareness

Before modifying core entities or shared services, AI agents must analyze:

* which modules depend on them
* whether the change affects existing workflows

Breaking existing functionality must be avoided.

---

# Implementation Planning

After repository discovery, AI agents should plan implementation using these steps:

1. Identify affected modules
2. Identify affected entities
3. Determine minimal implementation
4. Integrate with existing services

---

# Governance Check

AI1 may request repository discovery results before approving a feature implementation.

AI2 must be able to explain:

* where the change will occur
* why the module was selected
* why a new module is or is not required

This ensures architectural consistency across the system.
