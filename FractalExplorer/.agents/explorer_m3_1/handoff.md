# Handoff Report: Milestone 3 (sub-ViewModels Implementation) Investigation

## 1. Observation

A read-only investigation of the `MandelbrotExplorer` codebase was conducted. The following exact locations and states were observed:

1. **Target Files for Split**:
   - **`Fractal.UI/ViewModels/MainViewModel.cs`**:
     Spans 740 lines. Currently coordinates viewport zoom/pan/size/selection calculations, rendering parameters, ILGPU/Parallel generator execution, animation loop, file/clipboard operations, bookmarks, diagnostics properties, and culture settings.
   - **`Fractal.UI/ViewModels/NavigationViewModel.cs`**:
     Currently a stub with empty constructor and parameterized constructor accepting `IZoomService`, `BookmarkService`, and `ILogger<NavigationViewModel>` (lines 13-22):
     ```csharp
     public NavigationViewModel()
     {
     }

     public NavigationViewModel(IZoomService zoomService, BookmarkService bookmarkService, ILogger<NavigationViewModel> logger)
     {
         _zoomService = zoomService;
         _bookmarkService = bookmarkService;
         _logger = logger;
     }
     ```
   - **`Fractal.UI/ViewModels/DiagnosticsViewModel.cs`**:
     Currently a stub (lines 10-17):
     ```csharp
     public DiagnosticsViewModel()
     {
     }

     public DiagnosticsViewModel(ILogger<DiagnosticsViewModel> logger)
     {
         _logger = logger;
     }
     ```
   - **`Fractal.UI/ViewModels/RenderingViewModel.cs`**:
     Currently a stub (lines 13-22):
     ```csharp
     public RenderingViewModel()
     {
     }

     public RenderingViewModel(IFractalGenerator fractalGenerator, IZoomService zoomService, ILogger<RenderingViewModel> logger)
     {
         _fractalGenerator = fractalGenerator;
         _zoomService = zoomService;
         _logger = logger;
     }
     ```

2. **DI and Logging Packages Setup**:
   - **`Fractal.UI/Fractal.UI.csproj`**:
     Contains dependency injection and logging package references out of the box (lines 25-28):
     ```xml
     <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.8" />
     <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.8" />
     <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.0.8" />
     <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.8" />
     ```
   - **`Fractal.UI/App.axaml.cs`**:
     Already configures logging and registers all sub-ViewModels as transient services in DI (lines 30-36, 57-60):
     ```csharp
     collection.AddLogging(builder =>
     {
         builder.AddConsole();
         builder.AddDebug();
         builder.SetMinimumLevel(LogLevel.Information);
     });
     ...
     collection.AddTransient<NavigationViewModel>();
     collection.AddTransient<DiagnosticsViewModel>();
     collection.AddTransient<RenderingViewModel>();
     collection.AddTransient<MainViewModel>();
     ```

3. **Current Compile/Test Status**:
   - Running `dotnet test` fails due to compilation errors in `Fractal.Tests/UI/E2ETests.cs`:
     ```
     C:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\Fractal.Tests\UI\E2ETests.cs(155,12): error CS1061: Element „MainViewModel” nie zawiera definicji „ZoomOut” i nie odnaleziono dostępnej metody rozszerzenia „ZoomOut”...
     C:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\Fractal.Tests\UI\E2ETests.cs(741,16): error CS1061: Element „MainViewModel” nie zawiera definicji „ZoomOut”...
     C:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\Fractal.Tests\UI\E2ETests.cs(1538,12): error CS1061: Element „MainViewModel” nie zawiera definicji „ZoomOut”...
     ```
     This compilation failure occurs because in `MainViewModel.cs` line 395, `ZoomOut` is declared as `private void ZoomOut()` with a `[RelayCommand]` attribute. The source code generator creates a public property `ZoomOutCommand` but the backing method remains `private`, making direct calls to `vm.ZoomOut()` from the unit tests invalid.

---

## 2. Logic Chain

1. **Responsibility Splitting via MVVM (SRP Compliance)**:
   - Viewport navigation, mouse input mapping, coordinates, sizing, and bookmark management are grouped logically under `NavigationViewModel`.
   - Logging diagnostics telemetry (resolution, render duration, iteration count, engine name) is grouped under `DiagnosticsViewModel`.
   - Asynchronous fractal calculations, the animation loop, and file saving/clipboard interaction belong to `RenderingViewModel`.
   - `MainViewModel` coordinates these sub-ViewModels using events to maintain decoupling.

2. **Decoupled Coordination Design**:
   - Defining custom events ensures sub-ViewModels never reference each other directly.
   - `NavigationViewModel` raises a `RenderRequested` event when the user pans/zooms/resizes the canvas.
   - `RenderingViewModel` raises a `RenderRequested` event when the user alters fractal type, coloring palette, or Julia constants in the sidebar.
   - `MainViewModel` handles these events and invokes the asynchronous rendering cycle on the `RenderingViewModel`.
   - To update UI telemetry, `RenderingViewModel` fires `RenderCompleted(RenderCompletedEventArgs)` containing telemetry data (render time, iterations, engine name, viewport, zoom factor). `MainViewModel` handles this event to invoke diagnostic updates (`Diagnostics.UpdateStats`) and navigation text updates (`Navigation.UpdateViewportStats`).
   - Rather than using magic values in `RenderCompletedEventArgs` for export statuses (e.g. copy to clipboard, save to file), `RenderingViewModel` will raise `StatusMessageUpdated(string)` event to directly push status text updates to the `DiagnosticsViewModel` via `MainViewModel`.
   - To synchronize navigation with rendering when bookmarks are selected, `NavigationViewModel` raises `BookmarkSelected(BookmarkSelectedEventArgs)`. `MainViewModel` handles this by updating the rendering settings.

3. **Performance Optimization (Double Rendering Mitigation)**:
   - When a bookmark is selected, multiple parameters (FractalType, SelectedPalette, Iterations, Julia constants) are modified. If each parameter change independently triggers a `RenderRequested` event, it results in multiple redundant rendering operations.
   - To solve this, a batch update method `ApplySettings(...)` is introduced on `RenderingViewModel` which updates all backing properties directly, fires property changes for the UI, and raises a single `RenderRequested` event at the end.

4. **ILogger<T> Wiring Strategy**:
   - Injecting `ILogger<T>` into sub-ViewModels allows precise context-based logging:
     - `MainViewModel` logs language changes.
     - `NavigationViewModel` logs bookmark selection, creation, and deletion.
     - `RenderingViewModel` logs rendering requests (fractal generation, animation loop activation, image saving, clipboard operations) along with their current type/iterations, execution duration on completion, and any exceptions caught.

---

## 3. Caveats

- **Designer Support**: Parameterless constructors are necessary on all ViewModels to support the Avalonia Visual Designer. The parameterless constructors should instantiate services using mock/default implementations like `Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance` to prevent visual designer exceptions.
- **Debounced Panning**: The panning debounce timer (`_panDebounceTimer`) must remain inside the `NavigationViewModel` to prevent spamming renders on pointer drag, and it must properly dispatch the event on the Avalonia UI Thread (`Avalonia.Threading.Dispatcher.UIThread.Post`).

---

## 4. Conclusion & Strategy

The presentation layer should be refactored by moving the monolithic logic in `MainViewModel` to three distinct sub-ViewModels, using events for communication, standardizing telemetry logging, and adapting the views and tests to match.

### Proposed Sub-ViewModel Implementations

#### 1. `DiagnosticsViewModel.cs`
Responsible for visual telemetry.
- **Properties**:
  - `IsDiagnosticsVisible` (bool)
  - `StatusText` (string)
  - `ResolutionText`, `RenderTimeText`, `IterationsText`, `EngineText` (string)
- **Methods**:
  - `UpdateStats(long elapsedMs, int iterations, string engineName, Viewport viewport, double zoomFactor)`
  - `SetStatus(string status)`
- **Logging**: Inject `ILogger<DiagnosticsViewModel>` for diagnostic-specific issues.

#### 2. `NavigationViewModel.cs`
Responsible for navigation, sizing, coordinate mapping, and bookmarks.
- **Properties & Commands**:
  - `ViewportWidth`, `ViewportHeight` (int)
  - `IsSelecting` (bool), `SelectionRectangle` (Rect)
  - `IsPanning` (bool), `CanZoomOut` (bool)
  - `CenterCoordinatesText`, `SpanText`, `ZoomText`, `CursorCoordinatesText` (string)
  - `Bookmarks` (ObservableCollection), `SelectedBookmark` (BookmarkEntry), `NewBookmarkName` (string)
  - `ZoomOutCommand`, `ResetCommand`, `AddBookmarkCommand`, `DeleteBookmarkCommand` (RelayCommands)
- **Events & Delegates**:
  - `event EventHandler? RenderRequested`
  - `event EventHandler<BookmarkSelectedEventArgs>? BookmarkSelected`
  - `Func<(FractalType type, PaletteType palette, int iterations, double juliaReal, double juliaImag)>? GetRenderingDetails` (used to query active rendering settings when saving a bookmark).
- **Required Logging Actions**:
  - Bookmark Selected: `_logger.LogInformation("Navigating to bookmark: {Name}", value.Name);`
  - Bookmark Saved: `_logger.LogInformation("Saved new bookmark: {Name} (Type={Type}, Iterations={Iterations})", entry.Name, entry.FractalType, entry.Iterations);`
  - Bookmark Deleted: `_logger.LogInformation("Deleting bookmark: {Name}", bookmark.Name);`

#### 3. `RenderingViewModel.cs`
Responsible for fractal calculations, animation loop, and file export.
- **Properties & Commands**:
  - `FractalImage` (WriteableBitmap)
  - `SelectedPalette` (PaletteType), `SelectedFractalType` (FractalType)
  - `JuliaReal` (string), `JuliaImag` (string)
  - `IsJuliaSettingsVisible` (bool), `IsAnimating` (bool), `AdaptiveIterations` (int)
  - `GenerateFractalCommand` (AsyncRelayCommand)
  - `ToggleAnimationCommand`, `SaveImageCommand`, `CopyToClipboardCommand` (RelayCommands)
- **Events & Delegates**:
  - `event EventHandler? RenderRequested`
  - `event EventHandler? RenderStarted`
  - `event EventHandler<RenderCompletedEventArgs>? RenderCompleted`
  - `event EventHandler<RenderFailedEventArgs>? RenderFailed`
  - `event EventHandler<string>? StatusMessageUpdated` (passes text status updates directly to coordinator)
  - `Func<Task>? CopyToClipboardAction`, `Func<Task<string?>>? SaveFileDialogAction` (UI-level actions)
- **Required Logging Actions**:
  - Render Request: `_logger.LogInformation("Generating fractal. Type={Type}, Iterations={Iterations}", SelectedFractalType, AdaptiveIterations);`
  - Render Completed: `_logger.LogInformation("Render completed in {Duration} ms using {Engine}.", stopwatch.ElapsedMilliseconds, activeGenerator.Name);`
  - Render Failed: `_logger.LogError(ex, "Fractal generation failed.");`
  - Animation Toggle: `_logger.LogInformation("Animation loop toggled. IsAnimating={IsAnimating}", IsAnimating);`
  - Save Image Request: `_logger.LogInformation("Save image requested. Type={Type}, Iterations={Iterations}", SelectedFractalType, AdaptiveIterations);`
  - Save Image Completed: `_logger.LogInformation("Image saved successfully to {Path}", filePath);`
  - Save Image Failed: `_logger.LogError(ex, "Failed to save image.");`
  - Clipboard Request: `_logger.LogInformation("Copy to clipboard requested. Type={Type}, Iterations={Iterations}", SelectedFractalType, AdaptiveIterations);`
  - Clipboard Completed: `_logger.LogInformation("Copied image to clipboard.");`
  - Clipboard Failed: `_logger.LogError(ex, "Failed to copy image to clipboard.");`
- **Batch Update Method**:
  ```csharp
  public void ApplySettings(FractalType type, PaletteType palette, int iterations, string juliaReal, string juliaImag)
  {
      _selectedFractalType = type;
      _selectedPalette = palette;
      _adaptiveIterations = iterations;
      _juliaReal = juliaReal;
      _juliaImag = juliaImag;
      _isJuliaSettingsVisible = type == FractalType.Julia;

      OnPropertyChanged(nameof(SelectedFractalType));
      OnPropertyChanged(nameof(SelectedPalette));
      OnPropertyChanged(nameof(AdaptiveIterations));
      OnPropertyChanged(nameof(JuliaReal));
      OnPropertyChanged(nameof(JuliaImag));
      OnPropertyChanged(nameof(IsJuliaSettingsVisible));

      RenderRequested?.Invoke(this, EventArgs.Empty);
  }
  ```

#### 4. `MainViewModel.cs` (Orchestrator)
- **Properties**:
  - `Navigation` (NavigationViewModel)
  - `Diagnostics` (DiagnosticsViewModel)
  - `Rendering` (RenderingViewModel)
  - `Languages` (string[]), `SelectedLanguage` (string)
  - `IsSidePanelVisible` (bool)
  - `ToggleSidePanelCommand` (RelayCommand)
  - `ToggleFullscreenAction` (Action delegate)
- **Logging**:
  - Language Changed: `_logger.LogInformation("Language updated to {Language}", value);`
- **Constructor Event Hookups**:
  ```csharp
  public MainViewModel(NavigationViewModel navigation, DiagnosticsViewModel diagnostics, RenderingViewModel rendering, ILogger<MainViewModel> logger)
  {
      Navigation = navigation;
      Diagnostics = diagnostics;
      Rendering = rendering;
      _logger = logger;

      Navigation.RenderRequested += OnRenderRequested;
      Navigation.BookmarkSelected += OnBookmarkSelected;
      Navigation.GetRenderingDetails = () => (
          Rendering.SelectedFractalType,
          Rendering.SelectedPalette,
          Rendering.AdaptiveIterations,
          (double)Rendering.GetJuliaCReal(),
          (double)Rendering.GetJuliaCImag()
      );

      Rendering.RenderRequested += OnRenderRequested;
      Rendering.RenderStarted += OnRenderStarted;
      Rendering.RenderCompleted += OnRenderCompleted;
      Rendering.RenderFailed += OnRenderFailed;
      Rendering.StatusMessageUpdated += OnStatusMessageUpdated;

      SelectedLanguage = LocalizationService.Instance.CurrentCulture.Name.StartsWith("pl", StringComparison.OrdinalIgnoreCase) ? "PL" : "EN";
      OnRenderRequested(this, EventArgs.Empty);
  }
  ```
- **Coordination Handlers**:
  ```csharp
  private void OnRenderRequested(object? sender, EventArgs e) => _ = Rendering.GenerateFractalAsync();
  private void OnRenderStarted(object? sender, EventArgs e) => Diagnostics.SetStatus("Generating...");
  private void OnStatusMessageUpdated(object? sender, string msg) => Diagnostics.SetStatus(msg);
  private void OnRenderFailed(object? sender, RenderFailedEventArgs e) => Diagnostics.SetStatus($"Error: {e.ErrorMessage}");
  private void OnRenderCompleted(object? sender, RenderCompletedEventArgs e)
  {
      Diagnostics.UpdateStats(e.ElapsedMilliseconds, e.Iterations, e.EngineName, e.Viewport, e.ZoomFactor);
      Navigation.UpdateViewportStats(e.Viewport, e.ZoomFactor);
  }
  private void OnBookmarkSelected(object? sender, BookmarkSelectedEventArgs e)
  {
      Rendering.ApplySettings(
          e.Bookmark.FractalType,
          e.Bookmark.Palette,
          e.Bookmark.Iterations,
          e.Bookmark.JuliaCReal.ToString(CultureInfo.InvariantCulture),
          e.Bookmark.JuliaCImag.ToString(CultureInfo.InvariantCulture)
      );
  }
  ```

---

### Phase-by-Phase Implementation Steps

1. **Step 1: Implement `DiagnosticsViewModel.cs`**:
   Replace the class contents in `Fractal.UI/ViewModels/DiagnosticsViewModel.cs` with the visual diagnostics properties and constructors.
2. **Step 2: Implement `NavigationViewModel.cs`**:
   Replace `Fractal.UI/ViewModels/NavigationViewModel.cs` with coordinate tracking, sizing, mouse event inputs, and bookmark serialization. Ensure logging is added inside the bookmark commands and bookmark property setter. Make `ZoomOut()` and other zoom methods `public` so they are accessible.
3. **Step 3: Implement `RenderingViewModel.cs`**:
   Replace `Fractal.UI/ViewModels/RenderingViewModel.cs` with fractal generation logic, animation thread handling, and image export commands. Wire up logging calls at the start/completion/exception branches.
4. **Step 4: Refactor `MainViewModel.cs`**:
   Clean up the monolithic code by deleting migrated code. Keep language selection, side-panel toggling, sub-viewModel properties, and event coordination wire-ups.
5. **Step 5: Update XAML Views (`MainWindow.axaml`)**:
   Adjust data-binding pathways in `MainWindow.axaml` to reference sub-ViewModel structures.
   *Example mappings*:
   - `{Binding FractalImage}` $\to$ `{Binding Rendering.FractalImage}`
   - `{Binding ViewportWidth}` $\to$ `{Binding Navigation.ViewportWidth}`
   - `{Binding ZoomText}` $\to$ `{Binding Navigation.ZoomText}`
   - `{Binding EngineText}` $\to$ `{Binding Diagnostics.EngineText}`
   - `{Binding IsDiagnosticsVisible}` $\to$ `{Binding Diagnostics.IsDiagnosticsVisible}`
6. **Step 6: Update View Code-Behind (`MainWindow.axaml.cs`)**:
   Direct OS delegates and canvas pointer handlers to point to the sub-viewmodels (e.g. `vm.Navigation.OnPointerPressed(point.Position)` and `vm.Rendering.SaveFileDialogAction`).
7. **Step 7: Refactor Unit Tests and E2E Tests**:
   - In `Fractal.Tests/UI/MainViewModelTests.cs`, update unit tests to instantiate and run against `NavigationViewModel` directly.
   - In `Fractal.Tests/UI/E2ETests.cs`, fix the `vm.ZoomOut()` calls by using `vm.Navigation.ZoomOut()` (since `ZoomOut()` should be public in `NavigationViewModel`).
   - For all other tests in `E2ETests.cs`, update properties to reference their respective sub-ViewModel targets on the `MainViewModel`.
   *Test Property Mapping Table*:
   - `vm.ZoomText` $\to$ `vm.Navigation.ZoomText`
   - `vm.EngineText` $\to$ `vm.Diagnostics.EngineText`
   - `vm.IterationsText` $\to$ `vm.Diagnostics.IterationsText`
   - `vm.RenderTimeText` $\to$ `vm.Diagnostics.RenderTimeText`
   - `vm.ResolutionText` $\to$ `vm.Diagnostics.ResolutionText`
   - `vm.SpanText` $\to$ `vm.Navigation.SpanText`
   - `vm.CenterCoordinatesText` $\to$ `vm.Navigation.CenterCoordinatesText`
   - `vm.IsDiagnosticsVisible` $\to$ `vm.Diagnostics.IsDiagnosticsVisible`
   - `vm.SelectedPalette` $\to$ `vm.Rendering.SelectedPalette`
   - `vm.SelectedFractalType` $\to$ `vm.Rendering.SelectedFractalType`
   - `vm.JuliaReal` $\to$ `vm.Rendering.JuliaReal`
   - `vm.JuliaImag` $\to$ `vm.Rendering.JuliaImag`
   - `vm.IsJuliaSettingsVisible` $\to$ `vm.Rendering.IsJuliaSettingsVisible`
   - `vm.IsPanning` $\to$ `vm.Navigation.IsPanning`
   - `vm.IsSelecting` $\to$ `vm.Navigation.IsSelecting`
   - `vm.IsAnimating` $\to$ `vm.Rendering.IsAnimating`
   - `vm.CanZoomOut` $\to$ `vm.Navigation.CanZoomOut`
   - `vm.Bookmarks` $\to$ `vm.Navigation.Bookmarks`
   - `vm.SelectedBookmark` $\to$ `vm.Navigation.SelectedBookmark`
   - `vm.NewBookmarkName` $\to$ `vm.Navigation.NewBookmarkName`
   - `vm.StatusText` $\to$ `vm.Diagnostics.StatusText`
   - `vm.SelectionRectangle` $\to$ `vm.Navigation.SelectionRectangle`
   - `vm.ViewportWidth` $\to$ `vm.Navigation.ViewportWidth`
   - `vm.ViewportHeight` $\to$ `vm.Navigation.ViewportHeight`
   - `vm.StartPan(...)` $\to$ `vm.Navigation.StartPan(...)`
   - `vm.MovePan(...)` $\to$ `vm.Navigation.MovePan(...)`
   - `vm.EndPan()` $\to$ `vm.Navigation.EndPan()`
   - `vm.OnPointerPressed(...)` $\to$ `vm.Navigation.OnPointerPressed(...)`
   - `vm.OnPointerMoved(...)` $\to$ `vm.Navigation.OnPointerMoved(...)`
   - `vm.OnPointerReleased(...)` $\to$ `vm.Navigation.OnPointerReleased(...)`
   - `vm.CancelSelection()` $\to$ `vm.Navigation.CancelSelection()`
   - `vm.OnSizeChanged(...)` $\to$ `vm.Navigation.OnSizeChanged(...)`
   - `vm.OnMouseWheelZoom(...)` $\to$ `vm.Navigation.OnMouseWheelZoom(...)`
   - `vm.PanByPercent(...)` $\to$ `vm.Navigation.PanByPercent(...)`
   - `vm.ZoomCentered(...)` $\to$ `vm.Navigation.ZoomCentered(...)`
   - `vm.GenerateFractalCommand` $\to$ `vm.Rendering.GenerateFractalCommand`
   - `vm.SaveImageCommand` $\to$ `vm.Rendering.SaveImageCommand`
   - `vm.CopyToClipboardCommand` $\to$ `vm.Rendering.CopyToClipboardCommand`
   - `vm.ToggleAnimationCommand` $\to$ `vm.Rendering.ToggleAnimationCommand`
   - `vm.ResetCommand` $\to$ `vm.Navigation.ResetCommand`
   - `vm.AddBookmarkCommand` $\to$ `vm.Navigation.AddBookmarkCommand`
   - `vm.DeleteBookmarkCommand` $\to$ `vm.Navigation.DeleteBookmarkCommand`
   - `vm.SaveFileDialogAction` $\to$ `vm.Rendering.SaveFileDialogAction`
   - `vm.CopyToClipboardAction` $\to$ `vm.Rendering.CopyToClipboardAction`

---

## 5. Verification Method

To verify the implementation independently, perform the following checks:

1. **Build Verification**:
   Run `dotnet build` from `c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer`.
   - Ensure the solution builds successfully with zero compilation warnings or errors.
2. **Test Suite Execution**:
   Run `dotnet test` from `c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer`.
   - All tests in `Fractal.Tests` (including the refactored `MainViewModelTests.cs` and `E2ETests.cs`) must pass.
3. **Layout Compliance Check**:
   - Ensure `MainViewModel.cs` is reduced to coordination code and does not exceed ~200 lines.
   - Verify that all ViewModels are correctly instantiated by DI when running the app.
4. **Invalidation Conditions**:
   - Compilation errors in `MainWindow.axaml` due to unresolved path bindings.
   - Run-time exceptions in the Avalonia visual designer due to missing parameterless constructors on the ViewModels.
   - Multiple redundant renders triggered during bookmark selection.
