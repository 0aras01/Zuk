# Handoff: Color Palette System Stress Test & Verification (Milestone 1)

## 1. Observation
- The `GradientPalette.GetColor` math is vulnerable to underflow/overflow if a color stop sequence begins with a `t > 0` or ends with `t < 1.0`. Specifically, `blend = (t - s0.Position) / range` becomes negative, extrapolating RGB values into negative integers that, when cast to `byte` using unchecked operations, wrap around and cause visual glitches rather than clamping to 0 or 255.
- The `RunColorCyclingLoopAsync` logic within `RenderingViewModel.cs` uses `_iterationsBuffer` mapped via `totalPixels` based on `_lastWidth` and `_lastHeight`. The lengths of these buffers can be modified independently on a different thread by `GenerateFractalAsync`. Because the arrays are captured sequentially without memory barriers or locking, if a fractal render with a differing resolution commits new buffers just before `Parallel.For`, the color cycling thread may read past the bounds of `iterBuf` throwing an `IndexOutOfRangeException` and potentially crashing the background thread.
- `RunColorCyclingLoopAsync` indirectly invokes UI changes by triggering the `FractalImage` observer, interleaved with updates from `GenerateFractalAsync`, causing potential concurrent access exceptions on Avalonia's `WriteableBitmap.Lock()`.

## 2. Logic Chain
- A robust system under load must guard against unsynchronized buffer swaps.
- Because `GenerateFractalAsync` swaps `_iterationsBuffer` without thread locks, and does not atomically swap the width/height alongside the buffer, `totalPixels` calculated from `_lastWidth` can be larger than the new `_iterationsBuffer.Length` if the window is scaled down and then up, or vice versa.
- The `Parallel.For` in color cycling will attempt to read `iterBuf[i]`, crashing the loop if `i >= iterBuf.Length`. 
- Because `GradientPalette` math interpolates based on distance between two stops, and does not enforce a 0.0 stop or use clamped lerping, negative `blend` ratios cast to bytes create corrupt color output.

## 3. Caveats
- Evaluated via source analysis and C# test oracle harnesses. Some race conditions may manifest only under specifically timed UI actions (e.g., resizing while cycling).
- Assumes the default C# `unchecked` arithmetic behavior applies for byte conversions.

## 4. Conclusion
- **Verdict:** FAILED. 
- The Color Cycling system is not thread-safe. A race condition on `_iterationsBuffer` guarantees crashes when the application is resized under heavy load while animating. Gradient interpolation needs clamp operations (`Math.Clamp`). Both issues must be fixed before Milestone 1 is considered structurally complete.

## 5. Verification Method
- Execute `dotnet test c:\Users\Admin\source\repos\Zuk\FractalExplorer\FractalExplorer.slnx`
- Check `ColorPaletteStressTests.cs` (written to the test project)
- Manually resize the application window back and forth continuously while the "Color Cycling" toggle is enabled to trigger the buffer index crash.
