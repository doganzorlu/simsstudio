# UI Contract Component: Dropdowns & Filter Controls

This document defines dropdown selection controls and filter policy rules for SIMSStudio.

---

## 🎛️ Dynamic Loading Policy
Dropdown controls (e.g. resource type filters, game version selection) MUST follow the prohibition against eager loading anti-patterns:
- Do NOT trigger blocking I/O calls when populating dropdown options.
- Refer to [DROPDOWN_DYNAMIC_LOADING_POLICY.md](../AI_Governance/DROPDOWN_DYNAMIC_LOADING_POLICY.md) for full governance details.
