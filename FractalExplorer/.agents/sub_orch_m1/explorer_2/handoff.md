# Handoff Report: Color Palette System Implementation Plan

## Observation
- The existing hardcoded palette system resides in `Fractal.Core.Models.PaletteType` (enum with 4 values) and `Fractal.Core.Services.FractalCalculator.GetColor()`, which uses hardcoded cosine interpolation.
- A `GradientPalette` class exists in `Fractal.Core.Models.GradientPalette.cs` with linear color stop interpolation but lacks offset support for cycling.
- In `Fractal.Core.Services.ParallelFractalGenerator.cs`, the pixel buffer (`byte[] pixels`) is created inside `GenerateAsync` and iterations are immediately mapped to colors. The actual iteration values (`smoothIter`) are discarded after mapping.
- `Fractal.UI.ViewModels.RenderingViewModel` manages the active palette via `PaletteType _selectedPalette` and includes an animation loop (`RunAnimationLoopAsync`) currently used for zooming.
- UI elements for selecting palettes are in `MainWindow.axaml` (ComboBox bound to `Rendering.Palettes`).

## Logic Chain
1. **Model & Storage**: 
   - Delete `PaletteType.cs` and the cosine `GetColor` method in `FractalCalculator`.
   - Create a `PaletteService` in `Fractal.Core.Services` to manage `GradientPalette` models. This service will serialize/deserialize palettes using `System.Text.Json` to the local app directory (`AppDomain.CurrentDomain.BaseDirectory + "Palettes"`) and inject the 12 built-in aesthetic palettes.
   - Update `GradientPalette.GetColor` to support an offset for cycling: `t = (t + offset) - Math.Floor(t + offset);` (or `% 1.0`) before clamping.

2. **Decoupling Iteration from Rendering**: 
   - To achieve "Color Cycling without re-rendering", we must retain the intermediate iteration values. 
   - Modify `IFractalGenerator.GenerateAsync` to return `Task<(byte[] Pixels, double[] Iterations)>` (or create a `RenderResult` struct).
   - In `ParallelFractalGenerator`, store `smoothIter` in `iterations[offset]` and return it alongside the `byte[]` pixels.

3. **ViewModel Integration**:
   - Update `RenderingViewModel` to use `ObservableCollection<GradientPalette>` instead of `PaletteType[]`.
   - Cache `double[] _lastIterations` returned by the generator.
   - Add a `bool IsColorCycling` property. When true, launch `RunColorCyclingLoopAsync()`. This loop will continuously increment a `_paletteOffset`, run a `Parallel.For` over `_lastIterations` to compute new colors into `_pixelBuffer`, and update the `WriteableBitmap` directly, completely bypassing `GenerateAsync()`.

4. **UI Editor**:
   - Add a "Color Cycling" `CheckBox` in `MainWindow.axaml` bound to `Rendering.IsColorCycling`.
   - Create a new `PaletteEditorView.axaml` (and ViewModel) to edit `GradientPalette` stops. The view will feature a visual gradient bar (e.g., using a LinearGradientBrush), sliders/inputs for stop positions and RGB values, and Add/Remove/Save buttons.
   - Add an "Edit Palette" button next to the Palette ComboBox in `MainWindow.axaml` to launch this editor.

## Caveats
- **Memory Footprint**: Storing a `double[]` for a 1920x1080 viewport adds ~16MB of RAM. This is perfectly acceptable for normal use, but Milestone 3 (4K/8K rendering) will need to handle this buffer size carefully or stream it.
- **GPU Generator**: The `RenderingViewModel` references an `_gpuGenerator`. If a GPU implementation is fully implemented, it will also need to be updated to return the `double[]` buffer or implement color cycling natively on the GPU.

## Conclusion
The implementation requires decoupling the fractal mathematics from the color mapping. By changing `IFractalGenerator` to return both pixels and the raw iteration buffer, we enable the ViewModel to recolor the image at 60 FPS purely on the CPU using the `GradientPalette`. `PaletteService` will fulfill the JSON storage requirement, and the new UI views will expose the editor and color cycling functionalities.

## Verification Method
1. Inspect `Fractal.Core/Models/GradientPalette.cs` to ensure `GetColor` supports an offset parameter.
2. Verify `IFractalGenerator.GenerateAsync` signature returns the iteration array.
3. Run `dotnet build` and test the project: `dotnet test` to ensure generator changes do not break core math tests.
4. Launch the application, render a fractal, toggle "Color Cycling", and verify the palette animates smoothly without the "Generating..." overlay or high CPU iteration load.
