## 2026-06-05T17:29:23Z
You are the Worker for Milestone 3 (sub-ViewModels Implementation).
Working directory: c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\.agents\worker_m3\

Your tasks are:
1. Implement the three sub-ViewModels in c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\Fractal.UI\ViewModels\:
   - DiagnosticsViewModel.cs: Manages HUD performance stats (ResolutionText, RenderTimeText, IterationsText, EngineText, StatusText) and visibility (IsDiagnosticsVisible).
   - NavigationViewModel.cs: Manages viewport sizing (ViewportWidth, ViewportHeight), panning/zooming, selection rectangle, cursor coordinates text, and bookmarks. Ensure ZoomOut() and Reset() methods/commands are public.
   - RenderingViewModel.cs: Manages fractal calculation (GenerateFractalAsync), animation loop, file save (SaveImageAsync), and clipboard copy (CopyToClipboardAsync). Implement a batch settings update method to prevent duplicate/redundant rendering events when applying bookmark settings.

2. Refactor c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer\Fractal.UI\ViewModels\MainViewModel.cs:
   - Coordinate the sub-ViewModels using standard C# events (e.g., Navigation.RenderRequested, Rendering.RenderRequested, Rendering.RenderStarted, Rendering.RenderCompleted, Rendering.RenderFailed, Navigation.BookmarkSelected).
   - To keep the build compiling and all 34 tests passing (since View Integration and Test Refactoring are in future milestones), implement temporary compatibility properties/methods/commands in MainViewModel that delegate to the sub-ViewModels.
   - Ensure MainViewModel.cs remains under 300 lines of code.

3. Implement proper structured logging via ILogger<T> in each ViewModel, specifically:
   - Log rendering requests with type and iterations (Information).
   - Log render completions with duration (in ms) and engine used (Information).
   - Log exceptions for failed generation, failed save, and failed clipboard copy (Error).
   - Log bookmark actions: navigating to bookmark, adding new bookmark, and deleting bookmark (Information).
   - Log language changes (Information).
   - Log outputs must write to both Console and Debug outputs (already configured in App.axaml.cs DI).

4. Run `dotnet build` and `dotnet test` from the repository root to verify that the build compiles successfully with 0 warnings/errors and all 34 tests pass.

Write a handoff report to handoff.md in your working directory. When complete, send a message to the caller (ID: 1dff41c2-4496-4026-a450-d35e769a529a).

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.
