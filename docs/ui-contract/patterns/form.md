# UI Contract Pattern: Form Controls & File Input Workflows

This document defines form control layout standards and file path input workflows for SIMSStudio.

---

## 🗂️ File Path Input Pattern
Package inspection workflows accept input file paths via a clean `Grid` containing:
1. `TextBox` bound to `SelectedFilePath` with `PlaceholderText="Enter or paste .package file path..."`.
2. `Button` bound to `InspectCommand` with `Content="Inspect Package"`.
3. `ProgressBar` bound to `IsBusy` (`IsIndeterminate="True"`).

---

## 🔒 Action Guarding
The inspect action button MUST automatically disable (`IsEnabled="False"`) when `CanInspect` evaluates to `false` (`IsBusy == true` or `SelectedFilePath` is empty).
