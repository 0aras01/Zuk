# Handoff Report: Milestone 2 — DI & Log Configuration

## 1. Observation
- **Project Structure**:
  - `Fractal.UI/Fractal.UI.csproj` targets `<TargetFramework>net10.0</TargetFramework>` (line 4) and contains the following `<ItemGroup>` for PackageReferences (lines 15-26):
    ```xml
    <ItemGroup>
      <PackageReference Include="Avalonia" Version="12.0.3" />
      <PackageReference Include="Avalonia.Desktop" Version="12.0.3" />
      <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.3" />
      <PackageReference Include="Avalonia.Fonts.Inter" Version="12.0.3" />
      <PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.1">
        <IncludeAssets Condition="'$(Configuration)' != 'Debug'">None</IncludeAssets>
        <PrivateAssets Condition="'$(Configuration)' != 'Debug'">All</PrivateAssets>
      </PackageReference>
      <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
      <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.8" />
    </ItemGroup>
    ```
  - `Fractal.UI/App.axaml.cs` sets up Dependency Injection in the `OnFrameworkInitializationCompleted` method (lines 24-49):
    ```csharp
    public override void OnFrameworkInitializationCompleted()
    {
        // Dependency Injection Setup
        var collection = new ServiceCollection();

        // Try GPU acceleration first, fall back to CPU if unavailable
        collection.AddSingleton<IFractalGenerator>(sp =>
        {
            try
            {
                var gpu = new ILGPUFractalGenerator();
                Console.WriteLine($"[Fractal] GPU acceleration initialized: {gpu.Name}");
                return gpu;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Fractal] GPU initialization failed ({ex.Message}), falling back to CPU.");
                return new ParallelFractalGenerator();
            }
        });

        collection.AddSingleton<IZoomService, ZoomService>();
        collection.AddSingleton<BookmarkService>();
        collection.AddTransient<MainViewModel>();

        Services = collection.BuildServiceProvider();
        // ...
    }
    ```
  - **ViewModel files**: Currently, `Fractal.UI/ViewModels/` only contains `MainViewModel.cs` (verified via `list_dir`).
  - **Build and Test baseline**: Running `dotnet build` and `dotnet test` in `c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer` executes successfully with 0 warnings, 0 errors, and all 34 unit tests passing.

---

## 2. Logic Chain
- **Package Selection**: To configure logging in a `.NET 10` application using dependency injection, we need to install the logging library and its providers. Since `Microsoft.Extensions.DependencyInjection` is at version `10.0.8`, we should add `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.Console`, and `Microsoft.Extensions.Logging.Debug` at version `10.0.8` to ensure version matching and avoid dependency resolution warning/errors.
- **ServiceCollection Logging Setup**: To register logging, `AddLogging` must be called on `ServiceCollection`. Inside `AddLogging(Action<ILoggingBuilder> configure)`, we invoke `AddConsole()` and `AddDebug()` on the builder, and set the minimum level to `LogLevel.Information` using `SetMinimumLevel(LogLevel.Information)`. This requires the namespace `using Microsoft.Extensions.Logging;` in `App.axaml.cs`.
- **ViewModel Registration**:
  - The architectural plan requires four ViewModels: `NavigationViewModel`, `DiagnosticsViewModel`, `RenderingViewModel`, and the coordinating `MainViewModel`.
  - The current `MainViewModel` is registered as `Transient`. To maintain architectural consistency, prevent state leaks across multiple view resolutions, and enable clean instance creation, all four ViewModels should be registered as `Transient` (`AddTransient`).
  - The coordinating `MainViewModel` will depend on the other three ViewModels. By registering them as transient in the DI container, they will be injected directly into the `MainViewModel` constructor (along with logging: `ILogger<MainViewModel>`), maintaining standard DI patterns.
  - The sub-ViewModels will inject `IZoomService` and `BookmarkService` which are registered as singletons, ensuring a shared zoom history and bookmark configuration.

---

## 3. Caveats
- The new ViewModels (`NavigationViewModel`, `DiagnosticsViewModel`, and `RenderingViewModel`) do not exist in the codebase yet. Registering them in `App.axaml.cs` before creating their class definitions will cause a compilation error. Skeletal definitions of these classes must be created in the `Fractal.UI.ViewModels` namespace before compilation.
- The `App.axaml.cs` file initializes the GPU generator and logs messages directly using `Console.WriteLine`. We should refactor this to retrieve `ILogger<App>` from the service provider (`sp`) and log using structured methods (`LogInformation`, `LogError`).
- Test code (`Fractal.Tests/UI/MainViewModelTests.cs`) instantiates `MainViewModel` manually using its parameter-less or older 3-argument constructor. When VM restructuring is done, these tests will need to be refactored to construct the new ViewModels or mock them.

---

## 4. Conclusion
We have mapped the concrete steps required to add dependency injection and logging configuration for Milestone 2. 

### Step-by-Step Implementation Strategy:

1. **Step 1: Add NuGet dependencies to `Fractal.UI/Fractal.UI.csproj`**
   Add the following package references under the existing `<ItemGroup>`:
   ```xml
   <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.8" />
   <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.0.8" />
   <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.8" />
   ```
   *(See proposed patch file: `proposed_Fractal.UI.csproj.patch`)*

2. **Step 2: Create Skeleton ViewModel Classes**
   Create the files for the new ViewModels in `Fractal.UI/ViewModels/`:
   - `NavigationViewModel.cs`
   - `DiagnosticsViewModel.cs`
   - `RenderingViewModel.cs`
   Ensure they belong to the `Fractal.UI.ViewModels` namespace and inherit from `ObservableObject` (from `CommunityToolkit.Mvvm.ComponentModel`).

3. **Step 3: Refactor `App.axaml.cs` DI and Logging**
   - Add `using Microsoft.Extensions.Logging;` to imports.
   - Configure Logging inside the `ServiceCollection`:
     ```csharp
     collection.AddLogging(builder =>
     {
         builder.AddConsole();
         builder.AddDebug();
         builder.SetMinimumLevel(LogLevel.Information);
     });
     ```
   - Register the new ViewModels:
     ```csharp
     collection.AddTransient<NavigationViewModel>();
     collection.AddTransient<DiagnosticsViewModel>();
     collection.AddTransient<RenderingViewModel>();
     collection.AddTransient<MainViewModel>();
     ```
   - Update `IFractalGenerator` registration to resolve `ILogger<App>` and log GPU initialization state.
   *(See proposed patch file: `proposed_App.axaml.cs.patch`)*

---

## 5. Verification Method
1. **Compilation Check**: Run `dotnet build` from the workspace root (`c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer`). It must complete with 0 errors and 0 warnings.
2. **Unit Tests**: Run `dotnet test` to verify that all existing tests continue to pass.
3. **DI Verification**: Verify that the application boots and sets the `MainWindow.DataContext` correctly.
   - Place a breakpoint or debug statement inside `OnFrameworkInitializationCompleted` after `Services.GetRequiredService<MainViewModel>()` to ensure it resolves successfully along with its sub-ViewModels.
4. **Logging Verification**: Launch the application in Debug mode. Verify that messages like `GPU acceleration initialized` are output to both the Application Output/Debug window and the Console output.
