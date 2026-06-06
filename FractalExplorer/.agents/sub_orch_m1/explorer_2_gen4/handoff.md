# Handoff Report: Color Palette System Fixes (Milestone 1)

## Observation
1. **Concurrency Race Condition:** In `Fractal.UI/ViewModels/RenderingViewModel.cs`, `GenerateFractalAsync` updates `_iterationsBuffer` (line 244) and then updates `_lastWidth` / `_lastHeight` (lines 256-257) sequentially. Concurrently, `RunColorCyclingLoopAsync` calculates `totalPixels = _lastWidth * _lastHeight` (lines 373-376) and captures `_iterationsBuffer` (line 383). The `Parallel.For` loop (line 387) iterates up to `totalPixels`. Additionally, both methods call `_reusableBitmap.Lock()` (lines 260 and 417), with `GenerateFractalAsync` not explicitly isolating the lock in a thread-safe block against the background cycling loop's UI thread updates.
2. **Gradient Math Underflow:** In `Fractal.Core/Models/GradientPalette.cs`, `GetColor` calculates `double blend = range > 0 ? (t - s0.Position) / range : 0.0;` (line 62). If `t` is smaller than the first stop's position (`t < s0.Position`), `blend` becomes negative. This leads to negative values when calculating `r`, `g`, `b` (lines 64-66), resulting in unchecked casting of negative values to `byte`, which overflows/corrupts the colors.

## Logic Chain
1. **Race Condition:** Because `_iterationsBuffer` and the dimension fields (`_lastWidth`/`_lastHeight`) are updated non-atomically in `GenerateFractalAsync`, `RunColorCyclingLoopAsync` can read a newly updated, smaller `_iterationsBuffer` alongside old, larger dimensions. This causes `totalPixels` to exceed `iterBuf.Length`, leading to an `IndexOutOfRangeException` in `iterBuf[i]`. Furthermore, concurrent access to `_reusableBitmap.Lock()` can cause UI rendering deadlocks or crashes if the background cycling and the main renderer clash.
2. **Gradient Underflow:** The loop searching for the correct color stops breaks when `Stops[i + 1].Position >= t` (line 48). If `t` is close to 0 and the first stop is at `> 0`, `i` remains 0, making `s0 = Stops[0]`. Consequently, `t - s0.Position` is negative, calculating a negative `blend` ratio. Since C# casts to `byte` directly with truncation, a negative value wraps around to a high value, causing visual artifacts.

## Caveats
- `GenerateFractalAsync` depends on `SynchronizationContext.Current` implicitly via `await`. Depending on the environment, it may resume on the UI thread, but the lack of explicit synchronization for `_reusableBitmap` still causes problems if Avalonia UI tasks execute concurrently.

## Conclusion
- **Fix 1 (Race Condition):**
  - Group the iteration array and its dimensions into a single class/record or synchronize their updates.
  - In `RunColorCyclingLoopAsync`, bound the `Parallel.For` loop by `Math.Min(totalPixels, iterBuf.Length)` instead of strictly `totalPixels`.
  - Add an object lock `private readonly object _bitmapLock = new();` and wrap all `_reusableBitmap.Lock()` calls in `lock (_bitmapLock)` to ensure exclusive buffer writing.
- **Fix 2 (Gradient Underflow):**
  - In `GradientPalette.cs`, clamp the `blend` calculation so it never drops below 0 or exceeds 1:
    `double blend = Math.Clamp(range > 0 ? (t - s0.Position) / range : 0.0, 0.0, 1.0);`

## Verification Method
- **Race Condition:** Run the application, turn on "Color Cycling", and rapidly resize the window. Without the fix, the application crashes with `IndexOutOfRangeException`. With the fix, resizing should be stable.
- **Gradient Underflow:** Create a gradient palette where the first stop starts at `Position = 0.5`. Without the fix, colors will appear corrupted (e.g., pure white or arbitrary wrapped bytes) where `t < 0.5`. With the fix, it will safely clamp to the first stop's color.
