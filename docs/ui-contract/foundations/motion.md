# UI Contract Foundation: Motion & Loading Indicators (Avalonia Desktop)

This document defines desktop transition rules and progress indicator standards for SIMSStudio.

---

## ⚡ Principles
1. **Utility-Focused**: Animations in desktop developer tools must be fast, subtle, and utility-driven. Avoid flashy decorative animations.
2. **Indeterminate Progress Bar**: Long-running background inspection tasks (when `IsBusy == true`) MUST display an indeterminate `ProgressBar` (`IsIndeterminate="True"`) to communicate active processing.
3. **Thread Safety**: UI transitions MUST execute on the Avalonia UI thread; background I/O or binary inspection operations MUST be dispatched asynchronously via async/await (`InspectFileAsync`).

---

## ⏱️ Timing Guidelines
- **Fast Feedback (Hover/Focus)**: 100ms - 150ms.
- **Panel Visibility Toggle**: 200ms ease-in-out.
- **Progress Bar Indicator**: Immediate feedback on `IsBusy` change.
