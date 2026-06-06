# Scope: Mandelbrot Presentation Layer Refactoring

## Architecture
- Presentation layer refactoring: splitting MainViewModel into NavigationViewModel, DiagnosticsViewModel, and RenderingViewModel.
- DI and Logging configuration via App.axaml.cs and Microsoft.Extensions.Logging.
- View integration with bindings in MainWindow.axaml and MainWindow.axaml.cs.
- Unit and E2E test verification.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 2 | DI & Log Configuration | NuGet packages and App.axaml.cs registration | None | DONE |
| 3 | sub-ViewModels Implementation | Navigation, Diagnostics, and Rendering ViewModels | M2 | PLANNED |
| 4 | View Integration | MainWindow XAML and code-behind refactoring | M3 | PLANNED |
| 5 | Test Refactoring | Update MainViewModelTests.cs and run all tests | M4 | PLANNED |
| 6 | Adversarial Hardening | Tier 5 verification, warning/error check, constraints check | M5 | PLANNED |

## Interface Contracts
### MainViewModel ↔ sub-ViewModels
- Navigation.RenderRequested & Rendering.RenderRequested -> MainViewModel triggers Rendering.GenerateFractalAsync()
- Rendering.RenderStarted -> MainViewModel updates Diagnostics status
- Rendering.RenderCompleted -> MainViewModel updates Diagnostics and Navigation stats
- Navigation.BookmarkSelected -> MainViewModel updates Rendering properties
