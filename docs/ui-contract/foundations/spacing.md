# UI Contract Foundation: Spacing & Layout Grid (Avalonia Desktop)

This document defines spacing, padding, margin, and layout container standards for SIMSStudio desktop views.

---

## 📐 Spacing Scale
SIMSStudio follows an 8px base desktop grid (with 4px compact density steps for data-dense tables):

| Token Step | Metric | Usage |
| --- | --- | --- |
| `Space2` | 4px | Inner element padding, tight icon-text gaps |
| `Space3` | 8px | Standard control spacing, DataGrid cell padding |
| `Space4` | 12px | Grid row gaps, panel header margins |
| `Space5` | 16px | Outer window padding, container margins |
| `Space6` | 24px | Major section separators |

---

## 🖥️ Desktop Window Bounds
- **Default Window Width**: 800px (Minimum 600px).
- **Default Window Height**: 500px (Minimum 400px).
- **Control Height Standard**:
  - `TextBox` / `Button`: 32px height.
  - `DataGrid` Row: 28px - 32px height.

---

## 🚫 Rules
1. **Dynamic Layout Bounds**: Avoid hardcoding static absolute pixel offsets (e.g. `+12px` manual hacks) for dynamic content containers. Use Avalonia `Grid`, `DockPanel`, or `StackPanel` layout math.
2. **Stable Spacing**: Use `Thickness` values consistent with the 4/8/12/16px scale.
