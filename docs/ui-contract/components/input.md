# UI Contract Component: Input Controls & File Paths

This document defines text input controls and file path binding standards.

---

## ✏️ Input Field Rules
- **Placeholder Text**: Use `PlaceholderText="Enter or paste .package file path..."` (do NOT use obsolete `Watermark` property).
- **Two-Way Binding**: `TextBox.Text` must bind two-way to `SelectedFilePath` (`Text="{Binding SelectedFilePath}"`).
- **Validation**: Invalid paths update `StatusMessage` and display error styling on form submission.
