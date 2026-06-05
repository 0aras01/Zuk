# Handoff Report: Milestone 2 — DI & Log Configuration

## Summary of Core Findings
This report details the investigation of the dependency injection (DI) and logging configuration requirements for Milestone 2. We identified exactly where to add the required NuGet logging packages in `Fractal.UI.csproj`, how to set up console/debug logging at the `Information` level in `App.axaml.cs`, and a decoupled transient registration strategy for the new and existing ViewModels.

---

## 1. Observation
We observed the following relevant source and configuration details:

### A. Project Structure and Package References
In `Fractal.UI/Fractal.UI.csproj` (lines 15-26):
```xml
15:   <ItemGroup>
16:     <PackageReference Include="Avalonia" Version="12.0.3" />
17:     <PackageReference Include="Avalonia.Desktop" Version="12.0.3" />
18:     <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.3" />
19:     <PackageReference Include="Avalonia.Fonts.Inter" Version="12.0.3" />
20:     <PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.1">
21:       <IncludeAssets Condition="'$(Configuration)' != 'Debug'">None</IncludeAssets>
22:       <PrivateAssets Condition="'$(Configuration)' != 'Debug'">All</PrivateAssets>
23:     </PackageReference>
24:     <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
25:     <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.8" />
26:   </ItemGroup>
```
The project targets `.NET 10.0` (line 4) and references `Microsoft.Extensions.DependencyInjection` version `10.0.8`.

### B. Dependency Injection Setup in Application Lifecycle
In `Fractal.UI/App.axaml.cs` (lines 24-50):
```csharp
24:     public override void OnFrameworkInitializationCompleted()
25:     {
26:         // Dependency Injection Setup
27:         var collection = new ServiceCollection();
28: 
29:         // Try GPU acceleration first, fall back to CPU if unavailable
30:         collection.AddSingleton<IFractalGenerator>(sp =>
31:         {
32:             try
33:             {
34:                 var gpu = new ILGPUFractalGenerator();
35:                 Console.WriteLine($"[Fractal] GPU acceleration initialized: {gpu.Name}");
36:                 return gpu;
37:             }
38:             catch (Exception ex)
39:             {
40:                 Console.WriteLine($"[Fractal] GPU initialization failed ({ex.Message}), falling back to CPU.");
41:                 return new ParallelFractalGenerator();
42:             }
43:         });
44: 
45:         collection.AddSingleton<IZoomService, ZoomService>();
46:         collection.AddSingleton<BookmarkService>();
47:         collection.AddTransient<MainViewModel>();
48: 
49:         Services = collection.BuildServiceProvider();
```
We also observed that `Console.WriteLine` is used for diagnostics logging in lines 35 and 40 instead of structured logging.

### C. Existing ViewModels
Only `MainViewModel.cs` currently exists inside `Fractal.UI/ViewModels/`. The other ViewModels (`NavigationViewModel`, `DiagnosticsViewModel`, and `RenderingViewModel`) will be new creations, resulting from refactoring `MainViewModel.cs`.

---

## 2. Logic Chain

### A. Package References
1. **Goal**: Add NuGet logging packages `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.Console`, and `Microsoft.Extensions.Logging.Debug` at version `10.0.8`.
2. **Reasoning**: The existing DI package `Microsoft.Extensions.DependencyInjection` is at version `10.0.8`. Matching this version prevents assembly version mismatches and ensures full API compatibility under .NET 10.
3. **Location**: These should be appended as `<PackageReference>` elements inside the existing `<ItemGroup>` (lines 15-26) of `Fractal.UI/Fractal.UI.csproj`.

### B. Configuring ServiceCollection for Logging
1. **Goal**: Add logging to `ServiceCollection` in `App.axaml.cs` with `Console` and `Debug` providers, set minimum level to `LogLevel.Information`.
2. **Reasoning**:
   - In `App.axaml.cs`, the `collection` variable is a `ServiceCollection`. We can call `collection.AddLogging()` extension method to configure logging builders.
   - We must add `using Microsoft.Extensions.Logging;` to resolve the extension methods `AddLogging`, `AddConsole`, `AddDebug`, and the enum `LogLevel`.
   - In the builder configuration lambda, calling `builder.AddConsole()` and `builder.AddDebug()` configures these providers, and `builder.SetMinimumLevel(LogLevel.Information)` enforces the minimum log level.
3. **GPU Generator Factory Logging**:
   - The factory lambda for `IFractalGenerator` at lines 30-43 has access to the built service provider `sp`.
   - Instead of calling `Console.WriteLine`, we can resolve `ILogger<App>` (or `ILogger<IFractalGenerator>`) from `sp` via `sp.GetRequiredService<ILogger<App>>()` and write logs using `LogInformation` and `LogWarning` inside the catch block.

### C. ViewModel DI Registration Strategy
1. **Goal**: Register `NavigationViewModel`, `DiagnosticsViewModel`, `RenderingViewModel`, and `MainViewModel`.
2. **Analysis of Roles and Lifetimes**:
   - **MainViewModel**: The orchestration shell that embeds the child view models and coordinates application actions.
   - **NavigationViewModel**: Manages panning, zooming, and viewport coordinates.
   - **DiagnosticsViewModel**: Manages the display of render times, engine names, coordinate status, and iteration limits.
   - **RenderingViewModel**: Coordinates asynchronous fractal rendering and communication with `IFractalGenerator`.
3. **Lifetime Selection**:
   - Registering all ViewModels as **Transient** is highly recommended. ViewModels hold state representing the active UI components. Transient lifetime ensures that each time the UI is instantiated (or refreshed), a fresh, clean ViewModel is resolved, avoiding persistent stale UI state or memory leaks.
   - This matches the current transient registration of `MainViewModel` (line 47).
4. **Resolution via Constructor Injection**:
   - Registering them as transients allows the DI container to automatically resolve their constructors.
   - For example:
     - `NavigationViewModel` gets injected with `IZoomService` and `ILogger<NavigationViewModel>`.
     - `RenderingViewModel` gets injected with `IFractalGenerator`, `IZoomService`, and `ILogger<RenderingViewModel>`.
     - `DiagnosticsViewModel` gets injected with `ILogger<DiagnosticsViewModel>`.
     - `MainViewModel` gets injected with the child ViewModels: `NavigationViewModel`, `DiagnosticsViewModel`, `RenderingViewModel`, as well as `BookmarkService` and `ILogger<MainViewModel>`.

---

## 3. Caveats
- Since the child view models (`NavigationViewModel`, `DiagnosticsViewModel`, `RenderingViewModel`) are not yet created, we assume their standard UI-state tracking design.
- The concrete coordination mechanism (events vs message-passing via `IMessenger` from CommunityToolkit) between child view models is out of scope for this investigation but should be handled during the implementation phase.
- We assume that the log level filtering should apply programmatically for all configured providers.

---

## 4. Conclusion
1. **NuGet Additions** (`Fractal.UI/Fractal.UI.csproj`):
   ```xml
   <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.8" />
   <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.0.8" />
   <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.8" />
   ```
2. **DI Logging Configuration** (`Fractal.UI/App.axaml.cs`):
   - Import namespace: `using Microsoft.Extensions.Logging;`
   - Setup code:
     ```csharp
     collection.AddLogging(builder =>
     {
         builder.AddConsole();
         builder.AddDebug();
         builder.SetMinimumLevel(LogLevel.Information);
     });
     ```
   - Leverage `sp` inside the GPU generator factory lambda to log GPU state:
     ```csharp
     collection.AddSingleton<IFractalGenerator>(sp =>
     {
         var logger = sp.GetRequiredService<ILogger<App>>();
         try
         {
             var gpu = new ILGPUFractalGenerator();
             logger.LogInformation("GPU acceleration initialized: {GpuName}", gpu.Name);
             return gpu;
         }
         catch (Exception ex)
         {
             logger.LogWarning(ex, "GPU initialization failed, falling back to CPU.");
             return new ParallelFractalGenerator();
         }
     });
     ```
3. **ViewModel Registrations**:
   Register all view models as `Transient` right before building the service provider in `App.axaml.cs`:
   ```csharp
   collection.AddTransient<NavigationViewModel>();
   collection.AddTransient<DiagnosticsViewModel>();
   collection.AddTransient<RenderingViewModel>();
   collection.AddTransient<MainViewModel>();
   ```

---

## 5. Verification Method
1. **Compilation Check**:
   After applying the packages and code changes, verify that the project compiles cleanly using the command line:
   ```powershell
   dotnet build
   ```
2. **Runtime Verification**:
   - Run the application.
   - Verify the console and/or debug log output shows initialization logs (e.g. `GPU acceleration initialized: ...`).
   - Navigate and interact with the UI to verify that logs are populated dynamically when render tasks are triggered.
3. **Test Invalidation**:
   Ensure all unit tests continue to pass:
   ```powershell
   dotnet test
   ```
   If any VM constructor signature changes, update corresponding tests to pass mock dependencies correctly.
