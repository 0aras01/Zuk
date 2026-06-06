## 1. Observation
- `Fractal.UI/ViewModels/RenderingViewModel.cs` implements a concurrency fix within an anonymous lambda passed to `Avalonia.Threading.Dispatcher.UIThread.InvokeAsync` in the `RunColorCyclingLoopAsync` method.
- `Fractal.Tests/ColorPaletteStressTests.cs` (test `Concurrency_ColorCycling_RaceCondition_BufferLength`) does not instantiate `RenderingViewModel` or `MainViewModel`. Instead, it declares purely local dummy variables and mimics the locking/copying logic, ensuring a pass even if the real codebase is broken.
- The `Fractal.Tests.csproj` does not reference `Avalonia.Headless`. Any test that calls `viewModel.IsColorCycling = true` directly triggers the `Dispatcher.UIThread.InvokeAsync` call, which throws an exception in a standard xUnit environment because the UI thread is uninitialized.
- `MainViewModel.cs` is capable of securely instantiating all child ViewModels (including `RenderingViewModel`) without direct Avalonia UI context exceptions in its constructor.

## 2. Logic Chain
- The prior implementer created a facade test because running the actual `IsColorCycling` logic crashes xUnit without a mocked or initialized UI thread. 
- A self-certifying dummy test is an integrity violation because it does not exercise the target class's state or locking mechanisms.
- To test the real race condition without overhauling the test project architecture with Avalonia headless dependencies, the critical copying section inside the UI thread lambda must be decoupled from the `Dispatcher`.
- By extracting the `Marshal.Copy` lock block into an `internal void ApplyColorCyclingFrame(int width, int height)`, the test project can instantiate the real system (`new MainViewModel()`) and directly race `ApplyColorCyclingFrame` against `GenerateFractalAsync()`. This tests the exact state variables (`_stateLock`, `_reusableBitmap`, `_colorCyclingPixelBuffer`, etc.) on the real instance.

## 3. Caveats
- This strategy requires slightly modifying the internal structure of `RenderingViewModel` to extract a method purely for testability. However, this is a standard and clean testing practice.
- An alternative strategy would be adding `Avalonia.Headless.XUnit` and using `[AvaloniaFact]` to spin up the real dispatcher context, but extracting the method is cleaner and keeps test dependencies lightweight.

## 4. Conclusion
- To resolve the INTEGRITY VIOLATION, refactor `RenderingViewModel` by extracting the critical section inside `RunColorCyclingLoopAsync` into an `internal` method: `internal void ApplyColorCyclingFrame(int width, int height)`. 
- Rewrite `Concurrency_ColorCycling_RaceCondition_BufferLength` to instantiate the genuine `MainViewModel` (which provides a configured `RenderingViewModel`). 
- In the test, use parallel tasks: one repeatedly altering viewport sizes and calling `GenerateFractalAsync()`, and the other simulating the UI thread by repeatedly calling `ApplyColorCyclingFrame()`. Wrap both in `Record.ExceptionAsync` to assert that no exceptions like `ArgumentException` occur.

## 5. Verification Method
- **Implementer Action:** Extract `ApplyColorCyclingFrame(width, height)` in `RenderingViewModel.cs`. Update `ColorPaletteStressTests.cs` to instantiate `new MainViewModel()` and race the real methods.
- **Verification:** Run `dotnet test` on the test project.
- **Invalidation Condition:** To prove the test is no longer self-certifying, the developer can temporarily comment out the `lock(_stateLock)` or `_lastWidth == width` check in `ApplyColorCyclingFrame`. If the test correctly fails with a buffer or copy exception, the integrity of the test is verified.
