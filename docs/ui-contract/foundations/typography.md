# UI Contract Foundation: Typography (Avalonia Desktop)

This document defines typography rules, font scales, and monospaced hex representation standards for SIMSStudio desktop application.

---

## 🔤 Font Families
- **Primary Interface Font**: `Inter`, `Segoe UI`, `San Francisco`, or system default sans-serif font family.
- **Monospaced Data Font**: `Consolas`, `Cascadia Code`, `Menlo`, or system monospaced font family (used for Type, Group, Instance IDs and hexadecimal keys).

---

## 📏 Typography Scale (Desktop Density)

| Scale Name | Font Size | Font Weight | Line Height | Usage |
| --- | --- | --- | --- | --- |
| **Title Large** | 20pt / 26px | Bold (700) | 32px | Window main titles, primary view headers |
| **Title Medium** | 16pt / 20px | SemiBold (600) | 26px | Section headers, panel titles |
| **Body Regular** | 13pt / 16px | Normal (400) | 20px | Standard desktop body text, inputs |
| **Body Dense / Data**| 12pt / 15px | Normal (400) | 18px | DataGrid rows, table cells, metadata |
| **Monospaced Key** | 12pt / 15px | SemiBold (600) | 18px | Resource ID hex keys (`00B2D882:00000000:123456789ABCDEF0`) |
| **Caption / Status** | 11pt / 14px | Normal (400) | 16px | Status bar messages, helper text, warnings |

---

## 🚫 Rules
1. **Monospaced Hex Keys**: Binary offsets, Type/Group/Instance hex IDs, and formatted resource keys MUST use monospaced fonts (`FontFamily="Monospace"` or `Cascadia Code`) to prevent horizontal layout jank.
2. **Text Truncation**: Extended file paths or long status messages MUST configure `TextTrimming="CharacterEllipsis"` to prevent container clipping.
