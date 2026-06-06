# Investigation & Implementation Plan: Color Palette System Fixes

## 1. Missing Interactive Gradient Bar UI
**Observation**: 
In `Fractal.UI/Views/PaletteEditorWindow.axaml` (lines 15-43), the UI for editing the gradient relies on a `ListBox` and `NumericUpDown` inputs for Stop Position, R, G, and B. There is no visual representation of the gradient.
**Logic Chain**:
The current implementation only fulfills the data requirement but fails the interactive UI requirement. To provide a visual interactive gradient bar, the numeric inputs and list box must be replaced (or supplemented) by a custom visual control.
**Conclusion**:
Update `PaletteEditorWindow.axaml` to include a visual gradient bar (e.g., using a `Canvas` with an `ItemsControl` for draggable thumbs and a `LinearGradientBrush` background) that maps back to the `Position` property of the `GradientStopViewModel`.
**Verification Method**:
Run the application, open the Palette Editor, and verify a visual gradient bar is present and interactive.

## 2. Thread/Task Leak
**Observation**:
In `Fractal.UI/ViewModels/RenderingViewModel.cs` (lines 339-345), toggling `IsColorCycling` invokes `_ = RunColorCyclingLoopAsync()`. The `RunColorCyclingLoopAsync` (lines 347-400) loop relies purely on `while (IsColorCycling)`. 
**Logic Chain**:
Rapidly toggling the property `false -> true` launches new instances of `RunColorCyclingLoopAsync` before older loops can exit, as the condition `IsColorCycling` becomes true again. This results in multiple concurrent loops writing to `_pixelBuffer`, causing thread leaks and concurrency issues.
**Conclusion**:
Introduce a `CancellationTokenSource _colorCyclingCts;` in `RenderingViewModel`. In `OnIsColorCyclingChanged`, cancel the existing `_colorCyclingCts` and instantiate a new one before calling `RunColorCyclingLoopAsync(_colorCyclingCts.Token)`. Add `!token.IsCancellationRequested` to the loop condition.
**Verification Method**:
Toggle the "Color Cycling" option rapidly in the UI. Inspect diagnostic traces or the debugger to ensure only one loop task remains active.

## 3. Concurrency Crash (`IndexOutOfRangeException`)
**Observation**:
In `RenderingViewModel.GenerateFractalAsync` (lines 222-243), `_iterationsBuffer = result.Iterations;` is updated unconditionally. However, `_lastWidth` and `_lastHeight` are only updated inside the `if (!token.IsCancellationRequested)` block.
**Logic Chain**:
If a render is cancelled, `_iterationsBuffer` takes on the new size, but `_lastWidth` and `_lastHeight` retain their old values. The `RunColorCyclingLoopAsync` relies on `totalPixels = _lastWidth * _lastHeight` and indexes into `_iterationsBuffer`. If the old size was larger than the newly cancelled size, it throws an `IndexOutOfRangeException`.
**Conclusion**:
Move `_iterationsBuffer = result.Iterations;` inside the `if (!token.IsCancellationRequested)` block, specifically where `_lastWidth` and `_lastHeight` are updated, to ensure they remain synchronized.
**Verification Method**:
Rapidly resize the application window while Color Cycling is enabled to trigger cancellations, and verify no `IndexOutOfRangeException` is thrown.

## 4. UI Thread Blocking & Violations
**Observation**:
In `RenderingViewModel.RunColorCyclingLoopAsync` (lines 367-395), a CPU-intensive `Parallel.For` executes directly in the method. Because the method continues on the UI thread (default synchronization context), this blocks the UI thread. The subsequent UI updates (`_reusableBitmap.Lock()` and `FractalImage = tmp`) happen synchronously. If this were offloaded improperly, those UI updates would crash on a background thread.
**Logic Chain**:
To prevent blocking the UI thread, `Parallel.For` must be wrapped in `Task.Run()`. To safely update the UI, the bitmap lock and property reassignments must be explicitly marshalled back to the UI thread.
**Conclusion**:
Wrap `Parallel.For` inside `await Task.Run(() => { ... })`. Then wrap the bitmap update block inside `await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { ... })`.
**Verification Method**:
Enable Color Cycling and try interacting with the UI (e.g., resizing the window or hovering buttons). The UI should remain perfectly responsive.

## 5. Color Stops Sorting
**Observation**:
In `Fractal.UI/ViewModels/PaletteEditorViewModel.cs` (lines 55-60), `Save()` iterates over `Stops` exactly as they appear in the UI list and appends them to the newly created `GradientPalette`.
**Logic Chain**:
`GradientPalette.GetColor` implicitly assumes that gradient stops are sorted by `Position`. Unsorted stops lead to invalid interpolation and visual artifacts.
**Conclusion**:
Modify the `Save()` logic to sort the stops by `Position` before adding them to the new palette: `foreach (var s in Stops.OrderBy(x => x.Position))`.
**Verification Method**:
In the Palette Editor, add a stop with `Position = 0.5` after a stop with `Position = 0.9`. Save the palette and verify the generated fractal correctly interpolates colors without sharp, broken gradients.

## 6. Broken Tests
**Observation**:
1. `RenderingViewModel` parameterless constructor (`RenderingViewModel.cs:81-85`) instantiates `_paletteService` but never loads the palettes. 
2. `E2ETests.cs` fails `Tier1_Logging_LogsExpectedPhrases` because the `MainViewModel` test constructor (`MainViewModel.cs:262`) creates child ViewModels passing `null!` for their loggers, discarding all log output.
3. `Tier1_CancelRender_SlowGeneratorCancels` fails because it expects `vm.IsCancelVisible == true` after `200ms`, but `RenderingViewModel`'s cancellation logic delays for `5000ms` before setting it.
**Logic Chain**:
The empty `Palettes` collection causes crashes when resolving `SelectedPalette`. E2E logging tests expect console outputs that are suppressed by `null` loggers. The 5-second cancel overlay delay is unrealistic for tests (and users) expecting a 200ms response.
**Conclusion**:
- Update `RenderingViewModel`'s parameterless constructor to call `var loaded = _paletteService.LoadPalettes(); foreach(var p in loaded) Palettes.Add(p);` and select the first palette.
- In `E2ETests.cs`, construct `MainViewModel` by explicitly providing its dependencies (including loggers), rather than relying on the mismatched `MainViewModel` convenience constructor.
- Reduce the delay in `RenderingViewModel.GenerateFractalAsync` from `5000ms` to `200ms` or update the test to wait longer.
**Verification Method**:
Run `dotnet test` in the workspace. All E2E tests should pass successfully.
