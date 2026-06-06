# Challenge Report: Color Palette System Implementation (Milestone 1)

## Observation
1. Examined `RenderingViewModel.cs` for the "Color Cycling" implementation.
2. In `RenderingViewModel.cs`:
   ```csharp
   partial void OnIsColorCyclingChanged(bool value)
   {
       if (value)
       {
           _ = RunColorCyclingLoopAsync();
       }
   }

   private async Task RunColorCyclingLoopAsync()
   {
       while (IsColorCycling)
       {
           // [...] Does work and modifies _paletteOffset
           await Task.Delay(33); // ~30 fps
       }
   }
   ```
3. Attempted to run tests, but observed the logic permits multiple concurrent loops to spawn if the property is toggled quickly.
4. Examined `PaletteEditorViewModel.cs` and `GradientPalette.cs` for zero stops validation. `GradientPalette.cs` explicitly handles `Stops.Count == 0` by returning black, preventing crashes.

## Logic Chain
- The method `OnIsColorCyclingChanged` spawns a new fire-and-forget background task `RunColorCyclingLoopAsync()` every time `IsColorCycling` is set to `true`.
- Inside `RunColorCyclingLoopAsync()`, the loop continues as long as `IsColorCycling` evaluates to `true`. It sleeps for 33ms at the end of each iteration.
- If a user (or test) toggles `IsColorCycling` rapidly (`true` -> `false` -> `true`) within a 33ms window, the original loop is still sleeping. 
- When the original loop wakes up, it evaluates the `while (IsColorCycling)` condition, which is now `true` again, so it continues executing.
- Meanwhile, the latest toggle to `true` spawned a *second* background task. Both tasks are now running concurrently.
- If toggled rapidly N times, there will be N loops running in parallel. This causes:
  - `_paletteOffset` to increment N times faster.
  - Multiple concurrent `Parallel.For` executions overwriting the same `_pixelBuffer`, causing race conditions and tearing.
  - Huge CPU usage spike because of unbound concurrent infinite loops.
- As for the zero stops challenge, the implementation gracefully degrades to black color (`r = g = b = 0`), which is correct and does not crash the system.

## Caveats
- I attempted to execute a unit test to demonstrate the thread leak, but the `testhost` process from the previous run locked the binaries, so I relied on code analysis. The logic flaw is straightforward.

## Conclusion
**FAIL**. The implementation contains a critical concurrency bug (thread/task leak) when the "Color Cycling" toggle is spammed. Rapidly toggling the checkbox spawns multiple concurrent background tasks that never exit as long as the toggle remains true, leading to race conditions, visual tearing, and severe CPU usage. 

**Recommendation**: Store the `CancellationTokenSource` for the color cycling loop, cancel it when `IsColorCycling` becomes `false`, and ensure no previous loop is running before spawning a new one.

## Verification Method
1. Launch the UI.
2. Spam click the "Color Cycling" checkbox rapidly.
3. Observe that the animation speed multiplies exponentially and CPU usage spikes out of control.
4. Inspect `RenderingViewModel.cs` for the missing synchronization / cancellation logic.
