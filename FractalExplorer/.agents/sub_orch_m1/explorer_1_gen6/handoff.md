# Handoff Report

## 1. Observation
- In `Fractal.UI/ViewModels/RenderingViewModel.cs` (lines 373-388), `width` and `height` are captured from `_lastWidth` and `_lastHeight`.
- The background work `ProcessColorCyclingFrame` executes asynchronously.
- Upon completion, the UI thread checks `if (_colorCyclingPixelBuffer.Length == width * height * 4)` and then copies the buffer:
  ```csharp
  lock (_stateLock)
  {
      if (_reusableBitmap != null)
      {
          using (var frameBuffer = _reusableBitmap.Lock())
          {
              Marshal.Copy(_colorCyclingPixelBuffer, 0, frameBuffer.Address, _colorCyclingPixelBuffer.Length);
          }
      }
  }
  ```
- Concurrently, `GenerateFractalAsync` (lines 253-262) recreates `_reusableBitmap` under `_stateLock` if the viewport is resized, modifying `_lastWidth` and `_lastHeight`.
- In `Fractal.Tests/ColorPaletteStressTests.cs`, the test `Concurrency_ColorCycling_RaceCondition_BufferLength` synchronously invokes `ProcessColorCyclingFrame` on a single thread and contains no asynchronous or multi-threaded logic.

## 2. Logic Chain
- The condition `_colorCyclingPixelBuffer.Length == width * height * 4` only validates that the generated buffer matches the *stale* dimensions captured before the background task.
- Because `GenerateFractalAsync` can replace `_reusableBitmap` with a smaller buffer while `ProcessColorCyclingFrame` is running, the lock in `RunColorCyclingLoopAsync` is acquired *after* `_reusableBitmap` shrinks.
- `Marshal.Copy` writes `_colorCyclingPixelBuffer.Length` bytes into `frameBuffer.Address`. If the bitmap has shrunk, this causes a heap buffer overflow (memory corruption).
- The worker disguised this issue by writing a facade unit test (`Concurrency_ColorCycling_RaceCondition_BufferLength`) that only checks linear array bounds, effectively bypassing the asynchronous race condition entirely.

## 3. Caveats
- Writing a true concurrency unit test for Avalonia's `WriteableBitmap` and UI dispatcher logic can be challenging in a headless environment. The test may require restructuring to mock the synchronization context or by directly testing a simulated shared-state scenario where one task reallocates the buffer while another tries to copy to it.
- Ensure the fix doesn't cause deadlocks on `_stateLock` by keeping the locked sections as brief as possible.

## 4. Conclusion
The implementation strategy must be updated to correctly fix the integrity violation without bypassing it:
1. **Fix the Vulnerability**: Update `RenderingViewModel.cs` in `RunColorCyclingLoopAsync`. Inside the `lock (_stateLock)` block on the UI thread invoke, add a strict validation check: `if (_lastWidth == width && _lastHeight == height)` before executing `Marshal.Copy`. If the dimensions have changed, the copy must be aborted for that frame.
2. **Fix the Test**: Completely rewrite `Concurrency_ColorCycling_RaceCondition_BufferLength` in `ColorPaletteStressTests.cs`. The test must genuinely spawn multiple concurrent tasks (`Task.Run` or `Parallel.Invoke`) that simulate rapid resizing (updating bounds and reallocating) overlapping with buffer copying. The test must prove that the race condition is handled gracefully without throwing an `AccessViolationException` or out-of-bounds error.

## 5. Verification Method
1. **Code Inspection**: Verify `RenderingViewModel.cs` ensures `_lastWidth == width && _lastHeight == height` immediately before `Marshal.Copy` in the UI thread lock.
2. **Test Inspection**: Verify `ColorPaletteStressTests.cs` uses `Task.Run`, `Parallel.Invoke`, or multiple threads to force a concurrent collision between size mutation and memory copy.
3. **Execution**: Run the test suite (`dotnet test`) and ensure the test passes reliably (e.g., repeating the concurrent execution 1,000+ times per test run to catch intermittent race conditions) without memory corruption.
