# UI Contract Foundation: Colors & Brushes (Avalonia Desktop)

This document defines semantic color tokens and brush usage rules for SIMSStudio Avalonia UI application.

---

## 🎨 Semantic Token Mapping
In Avalonia XAML, views MUST bind to FluentTheme brushes via `{DynamicResource}` or `{StaticResource}` to preserve Light/Dark mode adaptability.

| Token Name | Theme Resource Key | Intended Usage |
| --- | --- | --- |
| `SystemControlBackgroundBaseLowBrush` | `SystemControlBackgroundBaseLowBrush` | Window and panel background |
| `SystemControlForegroundBaseHighBrush` | `SystemControlForegroundBaseHighBrush` | Primary text and headings |
| `SystemControlForegroundBaseMediumBrush` | `SystemControlForegroundBaseMediumBrush` | Secondary metadata and labels |
| `SystemAccentColor` | `SystemAccentColor` | Primary action highlights, active selection |
| `SystemControlErrorTextForegroundBrush` | `SystemControlErrorTextForegroundBrush` | Validation error text, fatal diagnostic issue |

---

## 🚦 Severity & Status Brushes
Resource inspection and conversion diagnostic issues must use standard status color brushes:

| Severity Level | Foreground Brush Resource / Color | Background Tint |
| --- | --- | --- |
| **Info** | `#0284C7` (Sky Blue) | `#F0F9FF` |
| **Warning** | `#D97706` (Amber/Orange) | `#FFFBEB` |
| **Error** | `#DC2626` (Red) | `#FEF2F2` |
| **Fatal** | `#7F1D1D` (Dark Red) | `#450A0A` |

---

## 🚫 Rules
1. **No Inline One-Off Hex Colors**: Do NOT write inline colors such as `Foreground="#123456"` on XAML controls. Use semantic ThemeResources.
2. **Accessible Contrast**: All text elements must achieve at least WCAG AA contrast ratio (4.5:1) against panel backgrounds.
