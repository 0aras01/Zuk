# Handoff Report: Color Palette System Fix Strategy

## 1. Missing Interactive Gradient Bar UI
**Observation:**
- `Fractal.UI/Views/PaletteEditorWindow.axaml` (lines 33-41) implements numeric inputs (`NumericUpDown`) for editing gradient stops instead of a visual interactive gradient bar.

**Logic Chain:**
- The requirements specify a "visual interactive gradient bar". The current implementation relies entirely on manual numeric data entry for positions and RGB values.

**Conclusion:**
- We must replace or augment the numeric inputs with a visual gradient bar in XAML (e.g., using a `Border` with a `LinearGradientBrush` bound to the color stops, and an `ItemsControl` for draggable stop thumbs).

**Verification Method:**
- Build and run the app. Open the Palette Editor. You should see a graphical bar representing the gradient, and you should be able to interact with it directly.

## 2. Thread/Task Leak
**Observation:**
- `Fractal.UI/ViewModels/RenderingViewModel.cs` (lines 339-345) spawns `_ = RunColorCyclingLoopAsync();` every time `OnIsColorCyclingChanged` receives `true`. It does not track the task or cancel previous instances.

**Logic Chain:**
- Rapid toggling of `IsColorCycling` launches multiple disconnected async loops. Because they run simultaneously and write to the same `_pixelBuffer` and UI properties, they cause a thread leak and race conditions.

**Conclusion:**
- We must add a `CancellationTokenSource` specifically for the color cycling loop. When `IsColorCycling` is set to true, cancel any existing token, create a new one, and pass it to `RunColorCyclingLoopAsync(CancellationToken token)`. The loop must exit when cancellation is requested.

**Verification Method:**
- Run the UI, toggle Color Cycling rapidly on and off. Check the memory and thread count in diagnostic tools to ensure no unbound loops persist.

## 3. Concurrency Crash (`IndexOutOfRangeException`)
**Observation:**
- `Fractal.UI/ViewModels/RenderingViewModel.cs` (lines 222-224) unconditionally assigns `_iterationsBuffer = result.Iterations;` *before* checking `!token.IsCancellationRequested`.

**Logic Chain:**
- If a render is cancelled quickly after size changes, `_iterationsBuffer` is overwritten with the new size, but `_lastWidth` and `_lastHeight` (updated on line 241) remain at the old dimensions. The color cycling loop uses `_lastWidth * _lastHeight` to index into the newly sized `_iterationsBuffer`, causing an `IndexOutOfRangeException` if the new buffer is smaller.

**Conclusion:**
- Move `_iterationsBuffer = result.Iterations;` and `byte[] pixelData = result.Pixels;` inside the `if (!token.IsCancellationRequested)` block, so buffers and dimension trackers are updated synchronously.

**Verification Method:**
- Run tests while rapidly resizing the window with Color Cycling enabled. Ensure no `IndexOutOfRangeException` is thrown.

## 4. UI Thread Blocking & Violations
**Observation:**
- `RunColorCyclingLoopAsync` (lines 347-400) executes `Parallel.For` directly in the async method, which runs on the UI thread due to the Avalonia synchronization context.

**Logic Chain:**
- `Parallel.For` performs heavy CPU computation and blocks the UI thread until completion. This freezes the UI. Furthermore, updates to `WriteableBitmap` (`_reusableBitmap.Lock()`) are interspersed without clear thread separation.

**Conclusion:**
- Offload the `Parallel.For` loop to a background thread using `await Task.Run(() => { Parallel.For(...) });`. After awaiting, update the `WriteableBitmap` and `FractalImage` back on the UI thread.

**Verification Method:**
- Run the app, enable Color Cycling, and attempt to interact with the UI (e.g., dragging the window or panning). The UI should remain perfectly responsive.

## 5. Color Stops Sorting
**Observation:**
- `Fractal.Core/Models/GradientPalette.cs` (lines 45-48) assumes stops are sorted by `Position`.
- `Fractal.UI/ViewModels/PaletteEditorViewModel.cs` (lines 53-60) copies stops exactly as ordered in the `Stops` list during `Save()`.

**Logic Chain:**
- If a user adds a stop with a position that is out of order, it is saved unsorted. The gradient interpolation loop will fail to correctly blend colors.

**Conclusion:**
- Modify `PaletteEditorViewModel.Save()` to sort the stops before appending them to the new palette: `foreach (var s in Stops.OrderBy(s => s.Position))`.

**Verification Method:**
- Open Palette Editor, add a stop with `Position = 0.5`, then another with `Position = 0.2`. Save and verify the rendered fractal colors do not exhibit interpolation glitches.

## 6. Broken Tests
**Observation:**
- `Fractal.UI/ViewModels/RenderingViewModel.cs` (lines 81-85) has a parameterless constructor that initializes `_paletteService` but does not load palettes. The parameterized constructor (line 93) does.

**Logic Chain:**
- E2E tests and `MainViewModel`'s parameterless constructor use the `RenderingViewModel` parameterless constructor. Because `Palettes` remains empty, `SelectedPalette` becomes null, crashing generation or test assertions.

**Conclusion:**
- Replicate the palette loading logic inside the parameterless constructor: call `_paletteService.LoadPalettes()` and populate the `Palettes` collection.

**Verification Method:**
- Run `dotnet test Fractal.Tests`. The `E2ETests` tier tests should now successfully instantiate models and find available palettes.

## Caveats
- Implementing the Interactive Gradient Bar UI will require custom Avalonia XAML logic (e.g., custom templating or dragging behaviors) which might be complex. The logic steps just identify *what* is missing, but the implementer will have to design the control.
- Ensure that `Task.Run` in the color cycling loop does not access UI-bound collections concurrently without dispatching.

