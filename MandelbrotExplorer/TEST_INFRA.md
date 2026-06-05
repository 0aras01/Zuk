# E2E Testing Infrastructure

This document outlines the testing infrastructure, design patterns, and test hierarchy used to perform End-to-End (E2E) integration testing on the presentation and interaction layers of the Mandelbrot Explorer.

## 1. Architectural Approach

The E2E test suite is implemented at the ViewModel level (`Fractal.Tests/UI/E2ETests.cs`) rather than via external visual UI automation tools (like Appium or WinAppDriver). This choice offers several benefits:
- **Execution Speed**: Tests execute in milliseconds instead of seconds, running the entire 126-test suite in under a second.
- **Environment Independence**: Avoids OS window focus issues, GPU driver requirements on headless CI environments, and windowing system dependencies.
- **Deterministic Awaiting**: Avoids arbitrary `Sleep` or `Delay` statements by directly awaiting the asynchronous command tasks (`vm.GenerateFractalCommand.ExecuteAsync(null)`).

### 1.1 Simulated GPU Generator Helper
To prevent headless CI systems from failing due to lack of a physical GPU (OpenCL/CUDA driver) while still validating the active engine-switching boundary conditions, the test suite defines a nested `SimulatedGpuGenerator` that implements `IFractalGenerator`:
- It exposes `IsGpuAccelerated = true` and `Name = "GPU (Simulated)"`.
- It internally delegates calculations to the CPU-based `ParallelFractalGenerator` to yield genuine pixel buffers.

### 1.2 State Isolation
The `BookmarkService` reads and writes to `bookmarks.json` in the user's `AppData` folder. To ensure tests do not pollute or delete actual user settings:
- The `E2ETests` constructor backs up any pre-existing `bookmarks.json` in AppData.
- The `E2ETests.Dispose()` method restores the original configuration and deletes any temporary files created during the run, providing absolute isolation.

---

## 2. Test Hierarchy (4-Tier Suite)

The suite contains exactly **126 distinct tests** organized into four tiers:

### Tier 1: Feature Coverage (55 Tests)
- **Zooming**: Verified via wheel scrolls, keyboard zooms, and stack navigation history.
- **Panning**: Verified via drag-pan state integration and arrow-key offset accumulation.
- **Selection**: Box selection dimensions, reverse drags, and aspect ratio matching.
- **Reset**: Restoration of default viewport, clearing history, and iteration resetting.
- **Bookmarks**: Saving custom views, loading presets, selection state synchronization.
- **Localization**: Interactive runtime culture updates, translations, key fallbacks.
- **Presets**: Setting palettes, fractal types, visibility checks, and palette enum coverage.
- **Julia Tuning**: Raw parsing of double-double parameters, boundary fallbacks.
- **Animation**: Toggling animation loops, coordinate updates, loop termination.
- **Diagnostics**: HUD telemetry formats, adaptive iteration speed adjustment.
- **Export/Clipboard**: Save-file dialog mocks, clipboard copy delegates, fallback directories.

### Tier 2: Boundary & Corner Cases (55 Tests)
- Testing of extreme zoom factors ($10^{15}$ depth) triggering engine switching.
- Out-of-bounds click/scroll positions, minimizing window (zero dimensions) during drag.
- Malformed bookmark JSON files, empty name validations, and invalid coordinate parses.
- Switch-cases on invalid Enum casts, rapid sequential keystrokes, and multi-threaded cancels.

### Tier 3: Cross-Feature Combinations (11 Tests)
Pairwise test scenarios such as zooming while panning, selecting bookmarks under alternate languages, resizing mid-animation, and adjusting parameters while Mandelbrot is active.

### Tier 4: Real-world Scenarios (5 Tests)
Simulated user journeys tracking multiple actions (e.g., loading a preset, drawing a zoom box, panning, updating Julia parameters, saving the image).

---

## 3. Execution Guide

To run the entire E2E test suite, execute the following command at the repository root:

```bash
dotnet test
```
