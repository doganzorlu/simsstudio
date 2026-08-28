# UI Contract Pattern: App Shell & Layout Structure

This document defines desktop app shell and panel layout patterns for SIMSStudio.

---

## 🖼️ Desktop App Shell Layout

The primary SIMSStudio window shell follows a clean `DockPanel` layout:

```text
+-----------------------------------------------------------------------+
|  Header Title & Tool Branding                                         |
+-----------------------------------------------------------------------+
|  File Input Path (TextBox) + Action Button (Inspect)                  |
|  [=== Progress Bar (IsVisible when IsBusy) ===]                      |
+-----------------------------------------------------------------------+
|  Status Message / Diagnostics Summary                                 |
+-----------------------------------------------------------------------+
|  Master Resource List / DataGrid                                      |
|  [ TypeHex | GroupHex | InstanceHex | Offset | Size | Compression ]     |
|                                                                       |
+-----------------------------------------------------------------------+
```

---

## 📐 Layout Controls
- **DockPanel**: Used for outer window regions (`DockPanel.Dock="Top"`, `DockPanel.Dock="Bottom"`).
- **Grid**: Used for file input controls with explicit `ColumnDefinitions="*, Auto"`.
- **ListBox / DataGrid**: Fills remaining available center region.
