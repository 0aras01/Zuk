# Original User Request

## Initial Request — 2026-06-05T19:21:10+02:00

Refactor the Fractal Explorer codebase to split the monolithic `MainViewModel` into sub-ViewModels and integrate structured logging.

Working directory: c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer
Integrity mode: development

## Requirements

### R1. Monolithic ViewModel Split
Split the large `MainViewModel` (currently over 600 lines) into cohesive sub-ViewModels:
- `NavigationViewModel`: Handles coordinate mapping, zoom history, mouse/keyboard zoom, pan offsets, and bookmark/preset management.
- `DiagnosticsViewModel`: Handles diagnostics HUD data (ZoomText, EngineText, RenderTimeText, ResolutionText, SpanText) and visibility toggles.
- `RenderingViewModel`: Handles `FractalImage` generation, cancellation tokens, adaptive iteration budgets, buffer reuse, and auto-zoom animations.
The `MainViewModel` should coordinate these sub-ViewModels. Update all XAML bindings in `MainWindow.axaml` and event handlers in `MainWindow.axaml.cs` to bind to the sub-ViewModels correctly.

### R2. Logging Integration
Integrate `Microsoft.Extensions.Logging` into the application:
- Log when render requests are initiated, completed (including rendering duration and engine used), and when exceptions are thrown.
- Log bookmark additions/deletions and language changes.
- Configure logging to write to both Debug and Console outputs.

### R3. Maintain Functionality & Compilation
All existing features (mouse wheel zoom, arrow-key panning, diagnostics toggle, bookmarks, perturbation theory speedup, auto-zoom animation) must work perfectly.

### R4. Floating Cancel Render Option
Add a floating "Cancel" button overlay on top of the image canvas.
- The cancel button must only become visible/enabled if the rendering operation takes longer than 5 seconds.
- Clicking the button must cancel the active render operation (CancellationTokenSource.Cancel()).
- The previously successfully rendered image must remain on the screen, and the status bar must display a localized message ("Render cancelled" / "Renderowanie anulowane").

## Acceptance Criteria

### Compilation & Tests
- [ ] The solution builds successfully with `dotnet build` with no warnings or errors.
- [ ] All automated tests in the test project pass with `dotnet test`.
- [ ] The monolithic `MainViewModel` code length is reduced to under 300 lines of code.
- [ ] A floating "Cancel" button overlay appears if rendering exceeds 5 seconds, and successfully cancels the active render (keeping the old image and showing the cancelled status).
