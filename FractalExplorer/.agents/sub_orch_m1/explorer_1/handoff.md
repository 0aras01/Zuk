# Milestone 1: Color Palette System Implementation Plan

## 1. Observation
- **Hardcoded Palettes**: The existing palette system uses an enum `PaletteType` (`Fractal.Core/Models/PaletteType.cs`) and is hardcoded procedurally in `FractalCalculator.GetColor()` (`Fractal.Core/Services/FractalCalculator.cs:285`).
- **Generators**: Both `ParallelFractalGenerator` and `ILGPUFractalGenerator` map iteration counts directly to RGB values within their computation loops and return a `byte[]` pixel buffer.
- **Rendering Loop**: `RenderingViewModel.GenerateFractalAsync()` receives the `byte[]` buffer and writes it to a `WriteableBitmap`.
- **UI Structure**: `MainWindow.axaml` has a right-side panel containing a `ComboBox` for Palettes. 

## 2. Logic Chain
- To replace the hardcoded system, we need a dynamic `GradientPalette` and `ColorStop` model, serializable to JSON.
- To achieve "Color Cycling without re-rendering the fractal", the fractal iteration calculation MUST be separated from the color mapping. If generators return `float[]` of `smoothIter` values instead of (or alongside) `byte[]` colors, the `RenderingViewModel` can cache these iterations. 
- A background task in `RenderingViewModel` can then continuously apply the offset `GradientPalette` to the cached `float[]` array, writing the new RGB values to the `WriteableBitmap` at 60fps without recalculating the heavy fractal math.
- The UI Editor requires an interactive way to define stops. This is best achieved via a new `PaletteEditorWindow` or popup overlay.

## 3. Caveats
- **Performance**: Returning `float[]` from the GPU takes slightly more bandwidth than `byte[]`, but it is still < 10ms for a 1080p image. A CPU `Parallel.For` can apply a LUT (Look-Up Table) to a `float[]` array into a `byte[]` array extremely fast (< 3ms), enabling real-time color cycling.
- **Tests**: Modifying `IFractalGenerator.GenerateAsync` to return `float[]` or a `FractalResult` object will require updating `E2ETests.cs` and `MemoryManagementTests.cs`.

## 4. Conclusion
We must split the fractal pipeline into two stages: Iteration Calculation -> Color Mapping. 

### Step-by-Step Implementation:
1. **Model & Storage**:
   - Create `ColorStop` (Offset, Color) and `GradientPalette` (Id, Name, Stops) in `Fractal.Core.Models`.
   - Add `GenerateLut(int size, double offset)` to `GradientPalette` for fast lookup.
   - Create `IPaletteService` with JSON serialization to load/save `Palettes.json` and initialize 12 default presets. Remove `PaletteType` enum.

2. **Core & Generators**:
   - Update `IFractalGenerator` to return a new `FractalResult` containing `float[] SmoothIterations` and `byte[] Pixels`. (Or just `float[]`).
   - Modify `ILGPUFractalGenerator` and `ParallelFractalGenerator` to populate and return the `float[]` iterations buffer instead of doing color mapping in the kernel.

3. **ViewModel & Color Cycling**:
   - In `RenderingViewModel`, cache the `float[] _iterationsBuffer`.
   - Implement `ApplyPaletteToBuffer(double offset)` which uses a `Parallel.For` to map iterations to colors using the active palette's LUT.
   - Add `bool IsColorCyclingEnabled` and an async loop that continuously increments the offset and redraws the bitmap.

4. **UI Editor**:
   - Add "Color Cycling" CheckBox and "Edit Palette..." Button under the Palette ComboBox in `MainWindow.axaml`.
   - Implement `PaletteEditorWindow.axaml` allowing users to select presets, edit/add/remove color stops, and save the gradient.

## 5. Verification Method
- **Build**: Run `dotnet build`.
- **Tests**: Ensure E2E tests pass (`dotnet test`).
- **Feature Check**: Run the app, select a new palette, ensure the UI updates. Toggle "Color Cycling" and verify the colors animate smoothly while the CPU/GPU usage remains low (confirming the fractal isn't being re-calculated).
