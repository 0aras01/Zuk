# Handoff Report

## 1. Observation
- In `Fractal.UI/ViewModels/RenderingViewModel.cs`, `RunColorCyclingLoopAsync` captures `width` and `height` based on `_lastWidth` and `_lastHeight` before moving to a background thread to compute the color cycling frame.
- After returning to the UI thread via `Avalonia.Threading.Dispatcher.UIThread.InvokeAsync`, it locks `_stateLock` and executes `Marshal.Copy(_colorCyclingPixelBuffer, 0, frameBuffer.Address, _colorCyclingPixelBuffer.Length)`.
- Concurrent execution of `GenerateFractalAsync` (e.g. from user zooming or window resizing) locks `_stateLock` and safely recreates `_reusableBitmap` and updates `_lastWidth`/`_lastHeight`. 
- If `GenerateFractalAsync` reduces the fractal resolution between the time `RunColorCyclingLoopAsync` captures `width` and `height` and the time it executes `Marshal.Copy`, the UI thread copies a larger `_colorCyclingPixelBuffer` into a smaller `frameBuffer.Address`, causing a heap buffer overflow.
- In `Fractal.Tests/ColorPaletteStressTests.cs`, the test `Concurrency_ColorCycling_RaceCondition_BufferLength` was rewritten to synchronously call the static method `ProcessColorCyclingFrame` to simulate a bounds check. It completely ignores `Avalonia.Threading.Dispatcher`, locks, and concurrent re-allocations of `WriteableBitmap`, acting as a facade test that bypassed the integrity requirement.

## 2. Logic Chain
- To actually fix the vulnerability, `RenderingViewModel` must verify that the dimensions haven't changed under its feet while it was computing the frame. Since `_lastWidth` and `_lastHeight` are strictly updated alongside `_reusableBitmap` under `_stateLock`, verifying `_lastWidth == width && _lastHeight == height` inside the lock before `Marshal.Copy` prevents the overflow.
- To genuinely test the race condition, a synchronous unit test is fundamentally inadequate because the vulnerability exists at the boundary of background `Task.Run` and Avalonia UI dispatcher queueing (`InvokeAsync`).
- We can recreate an authentic concurrency test by utilizing `AppBuilder` (like in `E2ETests.cs`) to initialize Avalonia context. Then, run `IsColorCycling = true` in a background thread while constantly resizing the window / triggering `GenerateFractalAsync` on another thread. Crucially, the main test loop must repeatedly call `Avalonia.Threading.Dispatcher.UIThread.RunJobs()` to forcefully pump the queued `Marshal.Copy` callbacks and collide them with the resize operations.

## 3. Caveats
- No caveats. The exact nature of the memory corruption vulnerability is fully diagnosed. 

## 4. Conclusion
**Proposed Fix Strategy:**

1. **Fix the Vulnerability (`RenderingViewModel.cs`):**
Modify line ~416 inside `RunColorCyclingLoopAsync` to validate dimensions before the memory copy:
```csharp
if (_colorCyclingPixelBuffer.Length == width * height * 4)
{
    lock (_stateLock)
    {
        // ADDED: _lastWidth == width && _lastHeight == height
        if (_reusableBitmap != null && _lastWidth == width && _lastHeight == height)
        {
            using (var frameBuffer = _reusableBitmap.Lock())
            {
                Marshal.Copy(_colorCyclingPixelBuffer, 0, frameBuffer.Address, _colorCyclingPixelBuffer.Length);
            }
        }
    }
}
```

2. **Fix the Facade Test (`ColorPaletteStressTests.cs`):**
Rewrite `Concurrency_ColorCycling_RaceCondition_BufferLength` to be a genuine multi-threading test. 
- Initialize Avalonia `AppBuilder` (copying the initialization logic from `E2ETests.cs` constructor).
- Initialize a `RenderingViewModel` and a simulated UI zoom loop.
- Set `IsColorCycling = true`.
- Run a background `Task.Run` loop that rapidly alternates generating smaller and larger fractal viewports.
- Run a `Task.Delay` timeout. While waiting, use `Avalonia.Threading.Dispatcher.UIThread.RunJobs()` on the main thread so that the color cycling `InvokeAsync` actions actually execute concurrently.
- Verify no `AccessViolationException` or memory corruption occurs.

## 5. Verification Method
1. Implement the real `Concurrency_ColorCycling_RaceCondition_BufferLength` test as described.
2. Run `dotnet test Fractal.Tests --filter ColorPaletteStressTests`. It should violently crash with an `AccessViolationException` / heap buffer overflow BEFORE the `RenderingViewModel.cs` fix is applied.
3. Apply the dimension check (`_lastWidth == width && _lastHeight == height`) fix to `RenderingViewModel.cs`.
4. Run the test again. It should now pass cleanly, proving the race condition was triggered and successfully mitigated.
