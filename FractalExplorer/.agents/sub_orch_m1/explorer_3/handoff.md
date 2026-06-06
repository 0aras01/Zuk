# Observation
- **Hardcoded Palette System**: Located in `Fractal.Core.Services.FractalCalculator.cs` (`GetColor` method, lines 286-330). The method uses hardcoded cosine-based palettes (Ice, Rainbow, Forest) driven by an `int paletteId`.
- **Existing Model**: `Fractal.Core.Models.GradientPalette` and `GradientStop` already exist but are not used by the rendering pipeline. They currently provide linear interpolation via a `GetColor` method but lack JSON attributes or a fast lookup table (LUT) generator.
- **Generator Coupling**: Both `ParallelFractalGenerator` and `ILGPUFractalGenerator` currently perform color mapping directly within their compute loops (or GPU kernel) and return a BGRA `byte[]`. 
- **UI Bindings**: `MainWindow.axaml` and `RenderingViewModel.cs` use a `PaletteType` enum. The `RenderingViewModel` updates the `WriteableBitmap` whenever `SelectedPalette` changes, which triggers a full recalculation of the fractal via `activeGenerator.GenerateAsync`.

# Logic Chain
1. **Dynamic Palettes & JSON Persistence**: To replace hardcoded IDs, we need a `PaletteService` that reads/writes `GradientPalette` collections to a `palettes.json` file. The service will inject 12 built-in palettes on first run.
2. **Decoupling Iteration from Coloring**: To support "animating the palette offset continuously *without* re-rendering the fractal", the fractal generators must stop computing colors. Instead:
   - Change `IFractalGenerator.GenerateAsync` to return `float[]` containing the `smoothIter` values.
   - `RenderingViewModel` will store this `float[] _lastIterations` buffer.
3. **High-Performance Color Mapping**: `GradientPalette` should be extended with a `BuildLookupTable(int size)` method returning a `uint[]` or `byte[]` array of colors. The `RenderingViewModel` will run a parallel loop over `_lastIterations` on the CPU, using the LUT and a `_paletteOffset` variable to write directly into `_pixelBuffer`.
4. **Color Cycling Toggle**: A simple `Task` loop in `RenderingViewModel` can continuously increment `_paletteOffset` and trigger the fast CPU-side color mapping without invoking the expensive fractal generators.
5. **Interactive UI**: A new `PaletteEditorView` and `PaletteEditorViewModel` must be created. The gradient bar can be implemented using an Avalonia `Canvas` drawing `GradientStops` as draggable thumbs.

# Caveats
- **Memory Footprint**: Returning `float[]` instead of `byte[]` requires allocating memory for smooth iteration counts. For a 1920x1080 viewport, a `float[]` requires ~8MB, which is negligible on modern systems, but changes the memory profile.
- **GPU Kernel Adjustment**: In `ILGPUFractalGenerator.cs`, the kernel signature must be updated to output a `float` ArrayView instead of `byte`. The ILGPU kernel must not perform color lookup.
- **Perturbation Scaling**: The smooth iteration values from CPU/GPU must not be clamped in the generators; they should just cap out at `maxIterations`. The color mapper will handle normalizing them to `[0.0, 1.0]`.

# Conclusion
The system requires an architectural split between Iteration Calculation and Color Mapping. By having generators return `float[]` buffers of iteration depths, `RenderingViewModel` gains the ability to instantly recolor the fractal using custom JSON-loaded `GradientPalette` objects, easily enabling smooth 60 FPS color cycling.

# Verification Method
1. Build the solution using `dotnet build` after implementing the new models and interfaces.
2. Run unit tests (`dotnet test`) ensuring `PaletteService` properly serializes and deserializes `palettes.json`.
3. Launch the application and select a new palette from the combobox. The view should update instantly without the "Generating..." progress overlay.
4. Toggle "Color Cycling"; the fractal colors should animate continuously while CPU usage remains very low (no calculation thread spinning).
5. Open the Palette Editor, drag a color stop, and observe the main viewport updating in real-time.
