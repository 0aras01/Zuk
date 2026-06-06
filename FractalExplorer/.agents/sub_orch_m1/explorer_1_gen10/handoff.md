## Observation
- The compilation of the project fails with `error CS0030: Nie można przekonwertować typu „Fractal.Core.Models.GradientPalette” na „int”` in `Fractal.UI/ViewModels/RenderingViewModel.cs` at line 195.
- The offending line is `int paletteId = (int)SelectedPalette;`.
- Looking at `Fractal.UI/ViewModels/RenderingViewModel.cs` lines 186-210, `paletteId` is never used. Instead, the `GenerateAsync` method at line 207 directly accepts `SelectedPalette` (which is of type `GradientPalette`).
- `IFractalGenerator.GenerateAsync` is defined as `Task<(byte[] Pixels, double[] Iterations)> GenerateAsync(Viewport viewport, int maxIterations, GradientPalette palette, double paletteOffset, FractalSettings settings, CancellationToken ct);`, taking a `GradientPalette` rather than an integer ID.

## Logic Chain
1. The error occurs because `SelectedPalette` is an object of type `GradientPalette`, which has no implicit or explicit conversion to `int`.
2. Inspecting the usage of the variable `paletteId` reveals it is completely unused. The subsequent call to `activeGenerator.GenerateAsync` passes `SelectedPalette` directly.
3. Therefore, this line is leftover code from before the `GradientPalette` model was introduced (when palettes were likely identified by an enum or integer).
4. The correct fix is to simply delete line 195 entirely. The prompt suggests replacing the invalid cast with the appropriate property of `GradientPalette` (or its index), so an alternative is replacing it with `int paletteIndex = Palettes.IndexOf(SelectedPalette);` if an integer index is absolutely needed for some reason, but given it is unused dead code, deleting it is the cleanest fix.

## Caveats
- No other files were checked for similar invalid casts, as the build output only identified this single error in `RenderingViewModel.cs`.

## Conclusion
The invalid cast in `RenderingViewModel.cs` on line 195 is an artifact of old code where palettes were represented as integers. The variable `paletteId` is no longer used, as `GenerateAsync` now takes the `GradientPalette` directly. The fix is to completely delete the line `int paletteId = (int)SelectedPalette;`. If keeping an index is strictly required by some unstated constraint, replace it with `int paletteIndex = Palettes.IndexOf(SelectedPalette);`, but its removal is fully sound.

## Verification Method
1. Edit `Fractal.UI/ViewModels/RenderingViewModel.cs` and remove line 195.
2. Run `dotnet build`.
3. Verify that the build succeeds without the `CS0030` error.
