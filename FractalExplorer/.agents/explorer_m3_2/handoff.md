# Handoff Report: Milestone 3 (sub-ViewModels Implementation) Investigation

## 1. Observation

A read-only investigation of the `MandelbrotExplorer` presentation layer was performed to analyze the sub-ViewModels refactoring plan. The direct observations are:

- **Target ViewModels**:
  - `Fractal.UI/ViewModels/NavigationViewModel.cs` is currently a skeleton class with empty/stub constructors (lines 7-23).
  - `Fractal.UI/ViewModels/DiagnosticsViewModel.cs` is currently a skeleton class with empty/stub constructors (lines 6-18).
  - `Fractal.UI/ViewModels/RenderingViewModel.cs` is currently a skeleton class with empty/stub constructors (lines 7-23).
  - `Fractal.UI/ViewModels/MainViewModel.cs` currently contains a monolithic implementation spanning 740 lines, handling zoom history, panning, selection, coordinate conversion, bookmarks, language locale updates, diagnostics telemetry, CPU vs GPU rendering execution, clipboard copy, and file saving.

- **Build and Test Status**:
  - Running `dotnet test` in `c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer` currently fails with 3 errors in `Fractal.Tests/UI/E2ETests.cs` because `MainViewModel` defines its `ZoomOut` method as private (using CommunityToolkit.Mvvm's `[RelayCommand]`), whereas `E2ETests.cs` calls it directly:
    ```
    C:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\Fractal.Tests\UI\E2ETests.cs(155,12): error CS1061: Element „MainViewModel” nie zawiera definicji „ZoomOut” i nie odnaleziono dostępnej metody rozszerzenia „ZoomOut”...
    C:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\Fractal.Tests\UI\E2ETests.cs(741,16): error CS1061: Element „MainViewModel” nie zawiera definicji „ZoomOut”...
    C:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\Fractal.Tests\UI\E2ETests.cs(1538,12): error CS1061: Element „MainViewModel” nie zawiera definicji „ZoomOut”...
    ```

- **Logging Configuration**:
  - `Fractal.UI/App.axaml.cs` has logging already configured via `collection.AddLogging()` (lines 31-36), using console/debug loggers at minimum level `LogLevel.Information`.
  - `Fractal.UI/Fractal.UI.csproj` already references the logging NuGet packages: `Microsoft.Extensions.Logging` (10.0.8), `Microsoft.Extensions.Logging.Console` (10.0.8), and `Microsoft.Extensions.Logging.Debug` (10.0.8) (lines 25-28).

- **View Interactivity**:
  - `Fractal.UI/Views/MainWindow.axaml` binds viewport sizes (`ViewportWidth`, `ViewportHeight`), overlays, and sidebar controls directly to `MainViewModel` fields.
  - `Fractal.UI/Views/MainWindow.axaml.cs` registers action delegates on the VM for OS operations (`SaveFileDialogAction`, `CopyToClipboardAction`, `ToggleFullscreenAction`), and routes pointer movements/resizes/keys directly to `MainViewModel` methods.

---

## 2. Logic Chain

1. **SRP and DI Separation**: To comply with the Single Responsibility Principle, the monolithic properties and methods must be split among three sub-ViewModels:
   - `NavigationViewModel`: owns sizing, canvas coordinate transformations, zoom history, panning, and bookmark management.
   - `DiagnosticsViewModel`: owns calculation/telemetry display variables.
   - `RenderingViewModel`: owns the rendering invocation loop, CPU/GPU selection, file storage, and clipboard exports.
2. **Coordinated Event-Driven Architecture**: Slicing the ViewModels creates independent scopes. To prevent coupling them directly, they must communicate via event handlers.
   - Viewing coordinates and canvas changes (managed by `NavigationViewModel`) should trigger a `RenderRequested` event.
   - UI configuration parameter adjustments (managed by `RenderingViewModel`) should also trigger a `RenderRequested` event.
   - The coordinator `MainViewModel` catches these events and schedules execution on `RenderingViewModel.GenerateFractalAsync()`.
   - On completion, `RenderingViewModel` fires `RenderCompleted`, carrying a custom payload of metrics that `MainViewModel` uses to update `DiagnosticsViewModel` and `NavigationViewModel` displays.
3. **Double-Rendering Prevention**: Simply updating the properties one-by-one inside `MainViewModel.OnBookmarkSelected()` will raise separate property-changed events for the fractal type, palette, and coordinates. Each property setter would raise a separate `RenderRequested` event, initiating multiple concurrent, redundant rendering tasks that abort each other.
   - *Reasoning*: A batch update method `ApplyFractalSettings()` must be introduced in `RenderingViewModel`. It will use a boolean suppression flag to disable `RenderRequested` triggers until all parameters are loaded, and fire exactly one single rendering event at the end.
4. **Logging Integration**: Since Microsoft logging is set up in `App.axaml.cs`, injecting `ILogger<T>` into each class gives precise logging at the point of action:
   - `NavigationViewModel`: logs bookmark load/addition/deletion and coordinates.
   - `RenderingViewModel`: logs rendering type/iterations, execution duration, engine name, file-saving destinations, and exceptions.
   - `MainViewModel`: logs culture localization adjustments.
5. **Ensuring Test/Code-Behind Backward Compatibility**: 
   - *Reasoning*: The existing tests (`E2ETests.cs` and `MainViewModelTests.cs`) compile against a parameterless constructor or `new MainViewModel(generator, zoomService, bookmarkService)`.
   - *Conclusion*: We must preserve a secondary constructor on `MainViewModel` that instantiates mock/empty sub-ViewModels and passes them through to prevent breaking the 110+ existing tests.
   - *Reasoning*: The three current compiler errors in `E2ETests.cs` are caused by the `ZoomOut()` helper being `private`. Exposing `ZoomOut()` and `Reset()` as `public` in `NavigationViewModel` (and delegating public methods in `MainViewModel` if necessary, or running commands) will resolve the errors.

---

## 3. Caveats

- **Avalonia Designer Constructor**: Avalonia's designer previewer executes the parameterless constructors. Sub-ViewModels must instantiate default fallback services (like `ParallelFractalGenerator` or `NullLogger`) in their parameterless constructors to prevent designer crashes.
- **Asynchronous Cancellation**: Since zoom animation loops and panning are highly interactive, proper propagation of `CancellationToken` in `RenderingViewModel` is required to ensure that a new render request cancels any pending calculation immediately.
- **Shared States**: Both `NavigationViewModel` and `RenderingViewModel` reference the singleton `IZoomService`. Care must be taken that viewport alterations in `NavigationViewModel` sync state safely before calculations run in `RenderingViewModel`.

---

## 4. Conclusion & Proposed Refactoring Strategy

We conclude that the monolithic `MainViewModel` can be successfully refactored into cohesive sub-ViewModels. We recommend implementing the following step-by-step strategy.

### Step-by-Step Implementation Plan

#### Step 1: Fix E2E Tests Compilation Mismatch
Modify `Fractal.Tests/UI/E2ETests.cs` to call the generated commands or make the underlying methods public. Specifically, replace direct private method calls like `vm.ZoomOut()` with command invocations:
- `vm.ZoomOut();` $\to$ `vm.ZoomOutCommand.Execute(null);` (or delegate via a public helper method in the ViewModel).

#### Step 2: Implement `DiagnosticsViewModel.cs`
Move all telemetry text fields here.
- **Properties**: `IsDiagnosticsVisible`, `StatusText`, `ResolutionText`, `RenderTimeText`, `IterationsText`, `EngineText`.
- **Methods**:
  - `UpdateStats(long elapsedMs, int iterations, string engineName, Viewport viewport, double zoomFactor)`
  - `SetStatus(string status)`
- **Logging**: Injected `ILogger<DiagnosticsViewModel>` can record telemetry update events.

#### Step 3: Implement `NavigationViewModel.cs`
Expose coordinates, pan tracking, and bookmark lists.
- **Properties**: `ViewportWidth`, `ViewportHeight`, `IsSelecting`, `SelectionStart`, `SelectionEnd`, `IsPanning`, `CanZoomOut`, `CenterCoordinatesText`, `SpanText`, `ZoomText`, `CursorCoordinatesText`, `Bookmarks`, `SelectedBookmark`, `NewBookmarkName`.
- **Commands**: `AddBookmarkCommand`, `DeleteBookmarkCommand`, `ZoomOutCommand`, `ResetCommand`.
- **Events**: `event EventHandler? RenderRequested` and `event EventHandler<BookmarkSelectedEventArgs>? BookmarkSelected`.
- **Logging**: Inside `AddBookmark()`, `DeleteBookmark()`, and `OnSelectedBookmarkChanged()`, log info statements via `ILogger<NavigationViewModel>`:
  - `"Navigating to bookmark: {Name}"`
  - `"Saved new bookmark: {Name}"`
  - `"Deleting bookmark: {Name}"`

#### Step 4: Implement `RenderingViewModel.cs`
Move calculation loops, animations, image byte writing, file saves, and clipboard copies here.
- **Properties**: `FractalImage`, `AdaptiveIterations`, `SelectedPalette`, `SelectedFractalType`, `JuliaReal`, `JuliaImag`, `IsJuliaSettingsVisible`, `IsAnimating`.
- **Events**: `RenderStarted`, `RenderRequested`, `RenderCompleted`, `RenderFailed`.
- **Prevent Duplicate Rendering (Batch Update)**:
  Provide a method to batch update properties without raising intermediate render requests:
  ```csharp
  private bool _isApplyingSettings;
  
  public void ApplyFractalSettings(FractalType fractalType, PaletteType palette, int iterations, string juliaReal, string juliaImag)
  {
      _isApplyingSettings = true;
      try
      {
          SelectedFractalType = fractalType;
          SelectedPalette = palette;
          AdaptiveIterations = iterations;
          JuliaReal = juliaReal;
          JuliaImag = juliaImag;
      }
      finally
      {
          _isApplyingSettings = false;
      }
      RenderRequested?.Invoke(this, EventArgs.Empty);
  }
  ```
  Ensure individual property change partial methods (like `OnSelectedPaletteChanged`) check `if (!_isApplyingSettings)` before invoking `RenderRequested`.
- **Logging**:
  - On start: `_logger.LogInformation("Generating fractal. Type={Type}, Iterations={Iterations}", SelectedFractalType, AdaptiveIterations);`
  - On success: `_logger.LogInformation("Fractal render completed. Duration={Duration}ms, Engine={Engine}", elapsedMs, engineName);`
  - On failure: `_logger.LogError(ex, "Fractal generation failed.");`
  - On save: `_logger.LogInformation("Image saved successfully to {Path}", filePath);` (and `LogError` on failure).
  - On clipboard: `_logger.LogInformation("Copied image to clipboard.");` (and `LogError` on failure).

#### Step 5: Implement orchestrator `MainViewModel.cs`
- Expose the three child ViewModels: `Navigation`, `Diagnostics`, and `Rendering`.
- **Properties**: `SelectedLanguage`, `IsSidePanelVisible`.
- **Events Wiring**:
  - Listen to `Navigation.RenderRequested` and `Rendering.RenderRequested` to invoke `Rendering.GenerateFractalAsync()`.
  - Listen to `Navigation.BookmarkSelected` and call `Rendering.ApplyFractalSettings(...)` with the bookmark properties.
  - Listen to `Rendering.RenderCompleted` to update Diagnostics and Navigation view stats.
- **Logging**: Log language switches: `_logger.LogInformation("Language updated to {Language}", value);`
- **Constructor Compatibility**: Preserve a constructor matching `public MainViewModel(IFractalGenerator, IZoomService, BookmarkService)` that forwards references to sub-ViewModels using mock/null loggers so all existing test setups continue to compile.

#### Step 6: Update View Bindings
- Adjust XAML binds in `MainWindow.axaml` to access child ViewModels (e.g. `{Binding FractalImage}` $\to$ `{Binding Rendering.FractalImage}`).
- In `MainWindow.axaml.cs`, redirect UI canvas mouse presses, mouse movements, mouse wheel zooms, size changes, key inputs, and dialog actions to the sub-ViewModels (e.g. `vm.Navigation.OnPointerPressed(...)` and `vm.Rendering.SaveFileDialogAction = ...`).

---

## 5. Verification Method

To verify the implementation of the refactored ViewModels:

1. **Compilation**:
   - Run `dotnet build` from `c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer`. Ensure it builds with 0 errors.
2. **Unit and E2E Tests**:
   - Run `dotnet test` from `c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer`. Confirm that all 110+ tests (including the fixed `E2ETests.cs`) pass successfully.
3. **Log Output Inspection**:
   - Run the application, click on presets/bookmarks, save an image, and change languages.
   - Inspect the standard console or debug logs and verify that the logs contain correct messages for:
     - Render starts with Type and Iterations.
     - Render completions with duration (in ms) and engine name.
     - Bookmark selections, additions, and deletions.
     - Language toggling ("PL" or "EN").
     - File path destinations and clipboard success messages.
4. **Invalidation Conditions**:
   - Compilation failures due to mismatched names or missing constructor signatures.
   - Run-time binding warnings in Avalonia output due to incorrect property paths in `MainWindow.axaml`.
   - Repeated rendering triggers when applying a bookmark.
