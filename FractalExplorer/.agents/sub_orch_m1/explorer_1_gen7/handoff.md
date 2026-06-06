# Investigation Handoff

## Observation
- `Fractal.Tests/ColorPaletteStressTests.cs` contains the test `Concurrency_ColorCycling_RaceCondition_BufferLength`, which was flagged by the Auditor as a facade test.
- Inspection of the test confirms it does not instantiate `RenderingViewModel`. Instead, it declares local variables (`sharedPixelBuffer`, `sharedWidth`, `sharedHeight`, `stateLock`) and runs a mocked simulation of the buffer copy logic.
- `RenderingViewModel.cs` uses `WriteableBitmap` and `Avalonia.Threading.Dispatcher.UIThread.InvokeAsync`.
- `Fractal.Tests.csproj` does not currently include the `Avalonia.Headless.XUnit` package, which explains why the previous agent bypassed instantiating the view model (xUnit lacks an Avalonia UI thread context by default).

## Logic Chain
1. To resolve the integrity violation, the test MUST instantiate and invoke the real application code (`RenderingViewModel`).
2. Because `RenderingViewModel` relies heavily on Avalonia-specific thread contexts and UI structures (`Dispatcher`, `WriteableBitmap`), attempting to instantiate it in a plain xUnit test causes exceptions.
3. The official and secure way to test Avalonia view models that interact with the UI thread is to use Avalonia's Headless testing platform.
4. By installing `Avalonia.Headless.XUnit`, the test framework can initialize a headless Avalonia environment. This provides the required `Dispatcher.UIThread` and allows `WriteableBitmap` to be allocated.
5. Once headless mode is enabled, the test can use the `[AvaloniaFact]` attribute instead of `[Fact]`.
6. Inside the `[AvaloniaFact]` test, we can genuinely instantiate `RenderingViewModel` with mocked dependencies (`Mock<IFractalGenerator>`, `Mock<IPaletteService>`), set `IsColorCycling = true`, and concurrently change the viewport resolution to trigger real race conditions on the actual `_stateLock` and buffer logic.

## Caveats
- Concurrency testing is inherently non-deterministic. The mocked `IFractalGenerator` might need to be configured to include slight delays (`Task.Delay`) to guarantee the race condition window overlaps reliably during the test.
- Setting up headless testing requires adding a package reference and a small setup class (e.g., `TestAppBuilder`) with an assembly-level attribute: `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]`.

## Conclusion
**Strategy to Fix Integrity Violation:**
Do not bypass the test. Instead, upgrade the test project to support Avalonia headless testing. Add `<PackageReference Include="Avalonia.Headless.XUnit" Version="12.0.3" />` to `Fractal.Tests.csproj`. Create a headless application builder setup, and rewrite `Concurrency_ColorCycling_RaceCondition_BufferLength` using `[AvaloniaFact]`. The rewritten test must instantiate `RenderingViewModel`, invoke `GenerateFractalAsync`, and toggle `IsColorCycling = true` while concurrently changing resolutions to trigger the actual codebase's buffer validation.

## Verification Method
1. Run `dotnet add c:\Users\Admin\source\repos\Zuk\FractalExplorer\Fractal.Tests\Fractal.Tests.csproj package Avalonia.Headless.XUnit --version 12.0.3`.
2. Inspect `ColorPaletteStressTests.cs` to ensure `var vm = new RenderingViewModel(...)` is genuinely created.
3. Verify the test asserts against the real code by running `dotnet test c:\Users\Admin\source\repos\Zuk\FractalExplorer\Fractal.Tests\Fractal.Tests.csproj` and checking that it executes without throwing unhandled buffer or dispatcher exceptions.
