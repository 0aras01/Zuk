# Handoff Report: Color Palette System (Milestone 1)

## Observation
1. **Concurrency Race Condition (`RenderingViewModel.cs`)**: In `RunColorCyclingLoopAsync` (lines 364-439), `totalPixels` is computed from `_lastWidth * _lastHeight`. The `Parallel.For(0, totalPixels, ...)` loop iterates over `_iterationsBuffer`. If `GenerateFractalAsync` replaces `_iterationsBuffer` with a smaller array due to window resizing before `_lastWidth` is updated, `totalPixels` exceeds `iterBuf.Length`, causing an `IndexOutOfRangeException`.
2. **Unsafe Bitmap Locking (`RenderingViewModel.cs`)**: `_reusableBitmap.Lock()` is called at line 260 within `GenerateFractalAsync` and at line 417 within `RunColorCyclingLoopAsync`. If `GenerateFractalAsync` executes on a background task context (e.g., triggered by property changes without a UI SynchronizationContext), it unsafely accesses the lock concurrently with the UI thread's color cycling loop.
3. **Gradient Math Underflow (`GradientPalette.cs`)**: At line 62, `double blend = range > 0 ? (t - s0.Position) / range : 0.0;`. If `t` drops below `Stops[0].Position`, `blend` evaluates to a negative number. This propagates to RGB interpolation `s0.R + (s1.R - s0.R) * blend`, causing negative double values that corrupt the byte cast due to unchecked underflow.

## Logic Chain
1. To address the race condition in the color cycling loop, the loop limit must be strictly bounded by the captured buffer's length. Using `int safePixels = Math.Min(totalPixels, iterBuf.Length);` ensures that array bounds are never violated, regardless of the sequence in which state fields are mutated.
2. To guarantee thread-safe memory copying to `WriteableBitmap`, a dedicated `_bitmapLock` object must be introduced. All blocks creating, reassigning, or locking `_reusableBitmap` must be enclosed within `lock (_bitmapLock)` to achieve mutual exclusion across concurrent tasks.
3. The gradient color math issue requires enforcing bounds on the `blend` calculation. By wrapping the logic with `Math.Clamp(..., 0.0, 1.0)`, we prevent negative blend factors when `t` sits before the first color stop, resolving the negative byte-casting bug.

## Caveats
- Bounding `safePixels` mathematically prevents crashes but may momentarily leave trailing edge pixels un-updated during rapid downscaling. However, the subsequent frame automatically corrects this once `GenerateFractalAsync` finishes syncing `_lastWidth`.
- Introducing `lock` objects on the UI thread for `WriteableBitmap` copy operations blocks execution momentarily. Given the raw buffer copying takes < 1ms, UI stuttering is not expected to be noticeable.

## Conclusion
The color cycling and gradient anomalies are caused by thread-unsafe memory accesses, loosely coupled state updates, and unclamped interpolation math. Securing the bitmap buffer with an explicit lock, clamping the total pixels iteration count, and applying `Math.Clamp` on the color blend definitively eliminate these crashes.

## Verification Method
1. **Tests**: Run `dotnet test` targeting `Fractal.Tests\ColorPaletteStressTests.cs`. `GradientPalette_Math_Underflow_Throws` will transition from failing to passing.
2. **Runtime Verification**: Initiate continuous color cycling. Vigorously resize the application window to trigger down-scaling and up-scaling. Ensure no `IndexOutOfRangeException` halts execution.

## Proposed Implementation Plan
1. **`Fractal.Core\Models\GradientPalette.cs`**:
   - Change line 62 to: `double blend = range > 0 ? Math.Clamp((t - s0.Position) / range, 0.0, 1.0) : 0.0;`
2. **`Fractal.UI\ViewModels\RenderingViewModel.cs`**:
   - Add field: `private readonly object _bitmapLock = new();`
   - In `GenerateFractalAsync`, wrap the `_reusableBitmap` reassignment and `Lock()` (lines ~249-264) in `lock (_bitmapLock) { ... }`.
   - In `RunColorCyclingLoopAsync`, declare `int safePixels = Math.Min(totalPixels, iterBuf.Length);`. Adjust `Parallel.For` to loop up to `safePixels`. Adjust buffer creation length verification to `< safePixels * 4`.
   - In `RunColorCyclingLoopAsync`, wrap the `_reusableBitmap.Lock()` block in `lock (_bitmapLock) { ... }`.
