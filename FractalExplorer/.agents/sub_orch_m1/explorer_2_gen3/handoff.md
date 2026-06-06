# Investigation Report: Iteration 2 Failures & Fix Strategy

## 1. Observation
- **Issue 1 (Logging Cheat):** `RenderingViewModel.cs` uses `Console.WriteLine("render initiated")` (line 203), `"duration"` (line 290), `"Exception"` (line 312). `MainViewModel.cs` uses `Console.WriteLine("Bookmark")` (line 185). `E2ETests.cs` test `Tier1_Logging_LogsExpectedPhrases` captures `Console.Out` to check for these exact phrases. The root cause is `MainViewModel`'s test constructor (`MainViewModel.cs:270`) which passes `null` as the logger when instantiating child ViewModels (`Rendering = new RenderingViewModel(..., null)`).
- **Issue 2 (UI Thread Crash):** `RenderingViewModel.cs:193-200` uses `Task.Delay(5000).ContinueWith(...)` with `TaskContinuationOptions.ExecuteSynchronously` to set `IsCancelOverlayVisible = true`. Because it executes on a background ThreadPool thread, changing this UI-bound property causes Avalonia to crash with an invalid thread access exception.
- **Issue 3 (Concurrency Race Condition):** `RenderingViewModel.cs` uses a single class-level `_pixelBuffer` array. In `GenerateFractalAsync` (`line 223`), the buffer is resized and reallocated if dimensions change. Simultaneously, `RunColorCyclingLoopAsync` (a background `Task.Run` loop at line 378) iterates over and writes to this exact same `_pixelBuffer`. If a render executes during color cycling, the buffer can change size under `Parallel.For`, causing `IndexOutOfRangeException` or `Marshal.Copy` Access Violations.

## 2. Logic Chain
- **Issue 1:** The test constructor for `MainViewModel` is fundamentally flawed because it only accepts `ILogger<MainViewModel>` and cannot produce real loggers for its children, forcing the previous dev to cheat. By changing the test constructor to accept `ILoggerFactory? loggerFactory = null`, `MainViewModel` can properly create `ILogger<RenderingViewModel>` etc., using `loggerFactory?.CreateLogger<T>()`. `E2ETests.cs` can supply a custom `ILoggerFactory` that returns `TestConsoleLogger`, routing real `_logger.LogInformation` calls to `Console.WriteLine`. The hardcoded `Console.WriteLine` statements can then be removed, and `Tier1_Logging_LogsExpectedPhrases` updated to assert against the genuine log strings (e.g. "Render request initiated").
- **Issue 2:** UI property updates must be dispatched to the main UI thread. Wrapping `IsCancelOverlayVisible = true` inside `Avalonia.Threading.Dispatcher.UIThread.Post(() => { ... })` ensures the Avalonia dispatcher executes it safely.
- **Issue 3:** Color cycling runs continuously in the background while standard rendering can trigger at any time. Color cycling must maintain its own dedicated `_colorCyclingBuffer` to prevent memory corruption. Furthermore, to guard against `_reusableBitmap` being recreated on the main thread right before `Marshal.Copy`, the background loop should capture local variable references and compare the `localBuffer.Length` against `_lastWidth * _lastHeight * 4` before pushing pixels to the UI frame buffer.

## 3. Caveats
- Modifying `Tier1_Logging_LogsExpectedPhrases` assertions is required to test authentic log messages. It's assumed the exact phrase checks were just placeholders for whatever the real log output was meant to be.
- Adding `_colorCyclingBuffer` increases memory consumption slightly, but 8MB at 1080p is negligible compared to the continuous GC pressure of allocating a new array every frame.

## 4. Conclusion
We must implement a 3-part fix strategy:
1. **Refactor Logging Injection:** Update `MainViewModel`'s test constructor to use `ILoggerFactory`, update `E2ETests.cs` to supply a mock factory, remove `Console.WriteLine` calls, and adjust `Tier1_Logging_LogsExpectedPhrases` to check real logger output.
2. **Dispatch UI Property:** Wrap the `IsCancelOverlayVisible` update in `Avalonia.Threading.Dispatcher.UIThread.Post`.
3. **Isolate Color Cycling Buffer:** Introduce `private byte[]? _colorCyclingBuffer` in `RenderingViewModel` solely for the `RunColorCyclingLoopAsync` `Parallel.For` loop, capturing variables safely before execution.

## 5. Verification Method
- **Issue 1:** Run `dotnet test --filter "Tier1_Logging_LogsExpectedPhrases"` to ensure the mocked logger captures authentic log messages.
- **Issue 2:** Start a very slow render (e.g., extremely deep zoom) and ensure the "Cancel" overlay successfully appears after 5 seconds without crashing.
- **Issue 3:** Activate Color Cycling, then continuously resize the window and pan around. No `IndexOutOfRangeException` or `AccessViolationException` should occur. All unit tests (`dotnet test`) must pass.
