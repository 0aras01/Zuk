# Project: MandelbrotExplorer Refactoring

## Architecture
This project refactors the presentation layer of the Mandelbrot Explorer application. It splits the monolithic `MainViewModel` into three cohesive sub-ViewModels under the MVVM pattern, using `CommunityToolkit.Mvvm`, integrates structured logging using `Microsoft.Extensions.Logging`, and adds a floating cancel render option.

- **MainViewModel**: Coordinates the sub-ViewModels. Acts as the primary DataContext for `MainWindow`.
- **NavigationViewModel**: Manages viewport dimensions, panning/zooming calculations, selection rectangles, and bookmark entries.
- **DiagnosticsViewModel**: Manages diagnostic telemetry HUD (Zoom, Engine, Iterations, Render Time, Resolution, Span) and visibility toggles.
- **RenderingViewModel**: Manages fractal image rendering, CPU/GPU generator dispatching, adaptive iterations logic, animation loop, file saving/clipboard sharing, and the floating cancel render overlay state and logic.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | E2E Testing Suite | Setup E2E testing infra and write feature/boundary/combination tests (Tiers 1-4) including Cancel button testing | None | IN_PROGRESS |
| 2 | DI & Log Configuration | Add Microsoft.Extensions.Logging dependencies and configure App DI container | None | DONE |
| 3 | sub-ViewModels Implementation | Implement Navigation, Diagnostics, and Rendering ViewModels, including logging and floating cancel logic | M2 | IN_PROGRESS |
| 4 | View Integration | Update bindings in MainWindow.axaml and event handlers in MainWindow.axaml.cs (including floating Cancel button overlay) | M3 | PLANNED |
| 5 | Test Refactoring | Update MainViewModelTests.cs to verify the new VM structure and pass all tests | M1, M4 | PLANNED |
| 6 | Adversarial Hardening | Implement Tier 5 coverage check, verify warnings/errors are 0, and confirm all constraints | M5 | PLANNED |

## Interface Contracts
### MainViewModel ↔ sub-ViewModels
- `MainViewModel` instantiates and holds instances of:
  - `NavigationViewModel Navigation`
  - `DiagnosticsViewModel Diagnostics`
  - `RenderingViewModel Rendering`
- Coordination is achieved using C# events:
  - `Navigation.RenderRequested` and `Rendering.RenderRequested` trigger `Rendering.GenerateFractalAsync()` via `MainViewModel`.
  - `Rendering.RenderStarted` updates diagnostics status.
  - `Rendering.RenderCompleted` updates stats in both `Diagnostics` and `Navigation`.
  - `Navigation.BookmarkSelected` updates selected properties in `Rendering`.

## Floating Cancel Render Option (R4)
- **Overlay UI**: A floating "Cancel" button overlay on top of the image canvas in `MainWindow.axaml`.
- **Visibility & Timer**: Button is bound to `Rendering.IsCancelButtonVisible`. Inside `RenderingViewModel`, when a render is initiated:
  - A timer is scheduled for 5 seconds.
  - If 5 seconds elapse and the render is still running, `IsCancelButtonVisible` is set to `true`.
  - When the render completes, fails, or is cancelled, the timer is cleared and `IsCancelButtonVisible` is set to `false`.
- **Cancellation**: Clicking the button triggers `Rendering.CancelRenderCommand`, calling `CancellationTokenSource.Cancel()`.
- **Fallback Image & Status**: The previously successfully rendered image remains displayed on the screen. The status bar displays the localized message: "Render cancelled" / "Renderowanie anulowane" retrieved from `LocalizationService`.

## Code Layout
- `Fractal.UI/ViewModels/MainViewModel.cs` - Coordinator ViewModel.
- `Fractal.UI/ViewModels/NavigationViewModel.cs` - Viewport and Bookmark management.
- `Fractal.UI/ViewModels/DiagnosticsViewModel.cs` - Performance stats and HUD.
- `Fractal.UI/ViewModels/RenderingViewModel.cs` - Fractal calculation, animation, and cancel option logic.
- `Fractal.UI/Views/MainWindow.axaml` - MainWindow layout, bindings, and cancel button overlay.
- `Fractal.UI/Views/MainWindow.axaml.cs` - MainWindow event handlers and OS delegates.
- `Fractal.Tests/UI/MainViewModelTests.cs` - UI ViewModel tests.
