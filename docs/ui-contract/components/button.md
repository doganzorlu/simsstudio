# UI Contract Component: Buttons & Action Triggers

This document defines button states and command triggers for SIMSStudio.

---

## 🔘 Button Variants
- **Primary Action Button**: Used for primary workflows (`Inspect Package`, `Convert Package`). Uses accent background (`SystemAccentColor`).
- **Secondary / Icon Button**: Used for utility actions (`Browse File...`, `Clear List`).

---

## 🔒 Disabled & Command Binding State
Avalonia `Button.Command` automatically disables the button when `CanExecute` evaluates to `false`. ViewModels MUST NOT manually toggle `Button.IsEnabled` in code-behind; rely on MVVM `RelayCommand` guard properties (`CanInspect`).
