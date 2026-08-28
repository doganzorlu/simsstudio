# UI Contract Pattern: Empty & Diagnostic States

This document defines empty states, unselected file placeholders, and diagnostic error states.

---

## 📭 Empty State Matrix

| State | Condition | Display Text / UI Behavior |
| --- | --- | --- |
| **No File Selected** | `SelectedFilePath == ""` | Status: `"Ready for package inspection."`, Resource list empty |
| **Empty Container** | `Resources.Count == 0 && IsSuccess == true` | Status: `"Inspection complete. Package container contains 0 resource entries."` |
| **Corrupt / Invalid**| `IsSuccess == false` | Status: `"Inspection failed: <Diagnostic Message>"`, Issues list populated |

---

## 🚫 Rules
1. **Never Mask Errors**: Corrupt or malformed package containers MUST NOT show a generic empty state without presenting diagnostic `ConversionIssue` records to the developer.
