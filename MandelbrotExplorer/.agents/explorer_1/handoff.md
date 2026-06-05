# Handoff Report: Fractal Explorer Refactoring Strategy

## 1. Observation

I performed a read-only investigation of the `MandelbrotExplorer` codebase and observed the following:

- **Build and Test Status**:
  - Running `dotnet build` in `c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer` successfully compiles all projects with 0 errors and 0 warnings:
    ```
    Kompilacja powiodła się.
        Ostrzeżenia: 0
        Liczba błędów: 0
    ```
  - Running `dotnet test` successfully completes with 34 tests passing and 0 failures:
    ```
    Powodzenie!    — niepowodzenie:     0, powodzenie:    34, pominięto:     0, łącznie:    34, czas trwania: 348 ms - Fractal.Tests.dll (net10.0)
    ```

- **MainViewModel.cs (`Fractal.UI/ViewModels/MainViewModel.cs`)**:
  - It spans 740 lines and coordinates several unrelated responsibilities:
    - **Navigation & Viewport**: Coordinates zoom states, panning, mouse interactions, sizing, coordinates text formatting (`CenterCoordinatesText`, `SpanText`, `ZoomText`, `CursorCoordinatesText`), and `BookmarkEntry` storage/retrieval.
    - **Diagnostics**: Manages properties for reporting rendering duration, active engine, iteration limit, and canvas resolution.
    - **Rendering**: Directly runs fractal computations, manages CPU vs GPU execution branches (`ParallelFractalGenerator` vs `_gpuGenerator`), allocates pixel byte arrays (`_pixelBuffer`), interacts with UI bitmap buffers (`_reusableBitmap`), runs the auto-zoom animation loop, and implements file-saving/clipboard actions.

- **MainWindow.axaml (`Fractal.UI/Views/MainWindow.axaml`)**:
  - Declares compiled bindings against `MainViewModel` (using `x:DataType="vm:MainViewModel"`).
  - Directly binds viewport controls (e.g. `ViewportWidth`, `ViewportHeight`, `FractalImage`, `IsSelecting`, `SelectionRectangle`) and diagnostic fields (e.g. `ZoomText`, `EngineText`, `IterationsText`, `RenderTimeText`, `ResolutionText`, `SpanText`, `CenterCoordinatesText`).

- **MainWindow.axaml.cs (`Fractal.UI/Views/MainWindow.axaml.cs`)**:
  - Directly injects actions into the DataContext for OS interaction (`SaveFileDialogAction`, `CopyToClipboardAction`, `ToggleFullscreenAction`).
  - Calls pointer/event methods directly on `MainViewModel` (e.g. `vm.OnPointerPressed`, `vm.StartPan`, `vm.ZoomOutCommand`, `vm.OnSizeChanged`, `vm.OnMouseWheelZoom`).

- **MainViewModelTests.cs (`Fractal.Tests/UI/MainViewModelTests.cs`)**:
  - Tests interaction behaviors like mouse selection box math and sizing layout changes against the monolithic `MainViewModel`.

- **App.axaml.cs (`Fractal.UI/App.axaml.cs`)**:
  - Sets up dependency injection via `Microsoft.Extensions.DependencyInjection` but lacks formal configuration of `Microsoft.Extensions.Logging`.

---

## 2. Logic Chain

1. **SRP Violation**: `MainViewModel` handles navigation, rendering, diagnostics, language configurations, bookmarks, and file I/O, violating the Single Responsibility Principle. This makes unit tests harder to maintain and write.
2. **Modularization benefits**: Slicing the ViewModel into:
   - `NavigationViewModel` (viewport logic, canvas interactions, bookmarks)
   - `DiagnosticsViewModel` (performance stats, visibility)
   - `RenderingViewModel` (fractal calculation, animation loop, file/clipboard operations)
   ...and orchestrating them via the parent `MainViewModel` provides a clean separation of concerns.
3. **Decoupled Event-Driven Coordination**: Rather than making view models directly invoke each other, we can use an event-driven design:
   - `NavigationViewModel` raises `RenderRequested` on viewport changes.
   - `RenderingViewModel` raises `RenderRequested` on palette/parameters changes.
   - `RenderingViewModel` raises `RenderCompleted` with render metadata once calculations finish.
   - The parent `MainViewModel` coordinates by listening to these events and transferring data (e.g. updating Diagnostics stats and Navigation viewport texts upon rendering completion).
4. **Microsoft.Extensions.Logging Integration**: Since the project already utilizes `Microsoft.Extensions.DependencyInjection` in `App.axaml.cs`, we can add `Microsoft.Extensions.Logging` directly to the `ServiceCollection` configuration. This allows injecting `ILogger<T>` into each of the refactored ViewModels, standardizing log output.

---

## 3. Caveats

- **Designer Support**: Avalonia's visual designer requires a parameterless constructor for `MainViewModel` (and sub-ViewModels). The designer constructors must supply empty/mock dependencies (like `NullLogger`) to prevent layout-time exceptions.
- **Resource Cleanup**: Slicing the rendering animation loop means handling state cancellation properly inside `RenderingViewModel` so that cancellation tokens are disposed when changing viewports or exiting the view.

---

## 4. Conclusion & Proposed Refactoring Strategy

This refactoring strategy will split the monolithic `MainViewModel` into cohesive sub-ViewModels, integrate logger support, and maintain compatibility with existing Avalonia controls and xUnit tests.

### Step 1: Install NuGet Packages and Configure Logging
In `Fractal.UI/Fractal.UI.csproj`, add package references for Microsoft Extensions Logging:
```xml
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.8" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.0.8" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.8" />
```

In `Fractal.UI/App.axaml.cs`, register Logging and the refactored ViewModels:
```csharp
public override void OnFrameworkInitializationCompleted()
{
    var collection = new ServiceCollection();

    // Configure Logging
    collection.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.AddDebug();
        builder.SetMinimumLevel(LogLevel.Information);
    });

    // Core services
    collection.AddSingleton<IFractalGenerator>(sp =>
    {
        try
        {
            var gpu = new ILGPUFractalGenerator();
            return gpu;
        }
        catch (Exception ex)
        {
            return new ParallelFractalGenerator();
        }
    });
    collection.AddSingleton<IZoomService, ZoomService>();
    collection.AddSingleton<BookmarkService>();

    // Register ViewModels
    collection.AddTransient<NavigationViewModel>();
    collection.AddTransient<DiagnosticsViewModel>();
    collection.AddTransient<RenderingViewModel>();
    collection.AddTransient<MainViewModel>();

    Services = collection.BuildServiceProvider();
    // ... setup classic desktop lifetime as usual ...
}
```

### Step 2: Implement `RenderingViewModel.cs`
Create `Fractal.UI/ViewModels/RenderingViewModel.cs` to handle calculations:
```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public class RenderCompletedEventArgs : EventArgs
{
    public long ElapsedMilliseconds { get; }
    public int Iterations { get; }
    public string EngineName { get; }
    public Viewport Viewport { get; }
    public double ZoomFactor { get; }

    public RenderCompletedEventArgs(long elapsedMs, int iterations, string engineName, Viewport viewport, double zoomFactor)
    {
        ElapsedMilliseconds = elapsedMs;
        Iterations = iterations;
        EngineName = engineName;
        Viewport = viewport;
        ZoomFactor = zoomFactor;
    }
}

public class RenderFailedEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public RenderFailedEventArgs(string errorMessage) => ErrorMessage = errorMessage;
}

public partial class RenderingViewModel : ObservableObject
{
    private readonly IFractalGenerator _gpuGenerator;
    private readonly IFractalGenerator _cpuGenerator = new ParallelFractalGenerator();
    private readonly IZoomService _zoomService;
    private readonly ILogger<RenderingViewModel> _logger;
    private CancellationTokenSource? _cts;

    private const double TargetRenderMs = 100.0;
    private const int MinIterations = 200;
    private const int MaxIterations = 50_000;

    [ObservableProperty]
    private int _adaptiveIterations = 500;

    private byte[]? _pixelBuffer;
    private WriteableBitmap? _reusableBitmap;
    private int _lastWidth, _lastHeight;

    [ObservableProperty]
    private WriteableBitmap? _fractalImage;

    public PaletteType[] Palettes { get; } = Enum.GetValues<PaletteType>();

    [ObservableProperty]
    private PaletteType _selectedPalette = PaletteType.Sunset;

    public FractalType[] FractalTypes { get; } = Enum.GetValues<FractalType>();

    [ObservableProperty]
    private FractalType _selectedFractalType = FractalType.Mandelbrot;

    [ObservableProperty]
    private string _juliaReal = "-0.7";

    [ObservableProperty]
    private string _juliaImag = "0.27015";

    [ObservableProperty]
    private bool _isJuliaSettingsVisible;

    [ObservableProperty]
    private bool _isAnimating;

    public Func<Task>? CopyToClipboardAction { get; set; }
    public Func<Task<string?>>? SaveFileDialogAction { get; set; }

    public event EventHandler? RenderStarted;
    public event EventHandler? RenderRequested;
    public event EventHandler<RenderCompletedEventArgs>? RenderCompleted;
    public event EventHandler<RenderFailedEventArgs>? RenderFailed;

    public RenderingViewModel(IFractalGenerator gpuGenerator, IZoomService zoomService, ILogger<RenderingViewModel> logger)
    {
        _gpuGenerator = gpuGenerator;
        _zoomService = zoomService;
        _logger = logger;
    }

    // Design-time fallback constructor
    public RenderingViewModel() : this(new ParallelFractalGenerator(), new ZoomService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<RenderingViewModel>.Instance)
    {
    }

    partial void OnSelectedPaletteChanged(PaletteType value)
    {
        _logger.LogInformation("Selected palette changed to {Palette}", value);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedFractalTypeChanged(FractalType value)
    {
        _logger.LogInformation("Selected fractal type changed to {FractalType}", value);
        IsJuliaSettingsVisible = value == FractalType.Julia;
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnJuliaRealChanged(string value) => RenderRequested?.Invoke(this, EventArgs.Empty);
    partial void OnJuliaImagChanged(string value) => RenderRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public async Task GenerateFractalAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        RenderStarted?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Generating fractal. Type={Type}, Iterations={Iterations}", SelectedFractalType, AdaptiveIterations);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var viewport = _zoomService.CurrentViewport;
            int iterations = AdaptiveIterations;
            double zoomFactor = 3.5 / (double)(viewport.Plane.RealMax - viewport.Plane.RealMin);

            var activeGenerator = (zoomFactor > 1e10 && _gpuGenerator.IsGpuAccelerated)
                ? _cpuGenerator
                : _gpuGenerator;

            int paletteId = (int)SelectedPalette;
            var settings = new FractalSettings(SelectedFractalType, GetJuliaCReal(), GetJuliaCImag());

            int requiredBytes = viewport.ImageWidth * viewport.ImageHeight * 4;
            if (_pixelBuffer == null || _pixelBuffer.Length != requiredBytes)
                _pixelBuffer = new byte[requiredBytes];

            byte[] pixelData = await activeGenerator.GenerateAsync(viewport, iterations, paletteId, settings, token);
            stopwatch.Stop();

            if (!token.IsCancellationRequested)
            {
                if (_reusableBitmap == null || _lastWidth != viewport.ImageWidth || _lastHeight != viewport.ImageHeight)
                {
                    _reusableBitmap = new WriteableBitmap(
                        new PixelSize(viewport.ImageWidth, viewport.ImageHeight),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Opaque);
                    _lastWidth = viewport.ImageWidth;
                    _lastHeight = viewport.ImageHeight;
                }

                using (var frameBuffer = _reusableBitmap.Lock())
                {
                    Marshal.Copy(pixelData, 0, frameBuffer.Address, pixelData.Length);
                }

                FractalImage = _reusableBitmap;

                // Adjust iterations dynamically
                double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                if (elapsedMs > 0)
                {
                    double ratio = TargetRenderMs / elapsedMs;
                    int proposed = (int)(iterations * ratio);
                    AdaptiveIterations = Math.Clamp((iterations + proposed) / 2, MinIterations, MaxIterations);
                }

                RenderCompleted?.Invoke(this, new RenderCompletedEventArgs(
                    stopwatch.ElapsedMilliseconds,
                    iterations,
                    activeGenerator.Name,
                    viewport,
                    zoomFactor
                ));
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Fractal generation cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fractal generation failed.");
            RenderFailed?.Invoke(this, new RenderFailedEventArgs(ex.Message));
        }
    }

    [RelayCommand]
    private void ToggleAnimation()
    {
        if (IsAnimating)
        {
            IsAnimating = false;
        }
        else
        {
            IsAnimating = true;
            _ = RunAnimationLoopAsync();
        }
    }

    private async Task RunAnimationLoopAsync()
    {
        while (IsAnimating)
        {
            var plane = _zoomService.CurrentViewport.Plane;
            DoubleDouble realRange = plane.RealMax - plane.RealMin;
            DoubleDouble imagRange = plane.ImagMax - plane.ImagMin;
            DoubleDouble centerReal = (plane.RealMin + plane.RealMax) * 0.5;
            DoubleDouble centerImag = (plane.ImagMin + plane.ImagMax) * 0.5;

            double factor = 0.97;
            DoubleDouble newRealRange = realRange * factor;
            DoubleDouble newImagRange = imagRange * factor;

            var newPlane = new ComplexPlane(
                centerReal - newRealRange * 0.5,
                centerReal + newRealRange * 0.5,
                centerImag - newImagRange * 0.5,
                centerImag + newImagRange * 0.5
            );

            _zoomService.ZoomTo(newPlane, _lastWidth, _lastHeight);
            RenderRequested?.Invoke(this, EventArgs.Empty);

            await GenerateFractalAsync();
        }
    }

    [RelayCommand]
    private async Task SaveImageAsync()
    {
        if (FractalImage == null) return;
        try
        {
            string? filePath = null;
            if (SaveFileDialogAction != null)
            {
                filePath = await SaveFileDialogAction();
            }

            if (string.IsNullOrEmpty(filePath))
            {
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SavedImages");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                string fileName = $"{SelectedFractalType.ToString().Replace(" ", "")}_Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                filePath = Path.Combine(folderPath, fileName);
            }

            FractalImage.Save(filePath);
            _logger.LogInformation("Image saved successfully to {Path}", filePath);
            RenderCompleted?.Invoke(this, new RenderCompletedEventArgs(-1, 0, filePath, _zoomService.CurrentViewport, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save image.");
            RenderFailed?.Invoke(this, new RenderFailedEventArgs($"Save error: {ex.Message}"));
        }
    }

    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        if (FractalImage == null || CopyToClipboardAction == null) return;
        try
        {
            await CopyToClipboardAction();
            _logger.LogInformation("Copied image to clipboard.");
            RenderCompleted?.Invoke(this, new RenderCompletedEventArgs(-2, 0, "", _zoomService.CurrentViewport, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy image to clipboard.");
            RenderFailed?.Invoke(this, new RenderFailedEventArgs($"Clipboard error: {ex.Message}"));
        }
    }

    public DoubleDouble GetJuliaCReal() => double.TryParse(JuliaReal, out double val) ? val : -0.7;
    public DoubleDouble GetJuliaCImag() => double.TryParse(JuliaImag, out double val) ? val : 0.27015;
}
```

### Step 3: Implement `NavigationViewModel.cs`
Create `Fractal.UI/ViewModels/NavigationViewModel.cs` to manage sizing, coordinates, panning, and bookmarks:
```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public class BookmarkSelectedEventArgs : EventArgs
{
    public BookmarkEntry Bookmark { get; }
    public BookmarkSelectedEventArgs(BookmarkEntry bookmark) => Bookmark = bookmark;
}

public partial class NavigationViewModel : ObservableObject
{
    private readonly IZoomService _zoomService;
    private readonly BookmarkService _bookmarkService;
    private readonly ILogger<NavigationViewModel> _logger;

    private Timer? _panDebounceTimer;
    private const int PanDebounceMs = 50;

    [ObservableProperty]
    private int _viewportWidth = 800;

    [ObservableProperty]
    private int _viewportHeight = 600;

    [ObservableProperty]
    private bool _isSelecting;

    [ObservableProperty]
    private Point _selectionStart;

    [ObservableProperty]
    private Point _selectionEnd;

    [ObservableProperty]
    private bool _isPanning;

    private Point _panStartPoint;
    private ComplexPlane _panStartPlane;

    [ObservableProperty]
    private bool _canZoomOut;

    [ObservableProperty]
    private string _centerCoordinatesText = "";

    [ObservableProperty]
    private string _spanText = "";

    [ObservableProperty]
    private string _zoomText = "";

    [ObservableProperty]
    private string _cursorCoordinatesText = "";

    public ObservableCollection<BookmarkEntry> Bookmarks { get; }

    [ObservableProperty]
    private BookmarkEntry? _selectedBookmark;

    [ObservableProperty]
    private string _newBookmarkName = "";

    public Rect SelectionRectangle => new Rect(
        Math.Min(SelectionStart.X, SelectionEnd.X),
        Math.Min(SelectionStart.Y, SelectionEnd.Y),
        Math.Abs(SelectionEnd.X - SelectionStart.X),
        Math.Abs(SelectionEnd.Y - SelectionStart.Y)
    );

    public event EventHandler? RenderRequested;
    public event EventHandler<BookmarkSelectedEventArgs>? BookmarkSelected;

    public Func<(FractalType type, PaletteType palette, int iterations, double juliaReal, double juliaImag)>? GetRenderingDetails { get; set; }

    public NavigationViewModel(IZoomService zoomService, BookmarkService bookmarkService, ILogger<NavigationViewModel> logger)
    {
        _zoomService = zoomService;
        _bookmarkService = bookmarkService;
        _logger = logger;

        _zoomService.Reset(ViewportWidth, ViewportHeight);
        Bookmarks = new ObservableCollection<BookmarkEntry>(_bookmarkService.LoadBookmarks());
        UpdateCanZoomOut();
    }

    public NavigationViewModel() : this(new ZoomService(), new BookmarkService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<NavigationViewModel>.Instance)
    {
    }

    partial void OnSelectionStartChanged(Point value) => OnPropertyChanged(nameof(SelectionRectangle));
    partial void OnSelectionEndChanged(Point value) => OnPropertyChanged(nameof(SelectionRectangle));

    partial void OnSelectedBookmarkChanged(BookmarkEntry? value)
    {
        if (value == null) return;
        _logger.LogInformation("Navigating to bookmark: {Name}", value.Name);
        _zoomService.ZoomTo(value.Plane, ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        BookmarkSelected?.Invoke(this, new BookmarkSelectedEventArgs(value));
    }

    partial void OnNewBookmarkNameChanged(string value) => AddBookmarkCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanAddBookmark))]
    private void AddBookmark()
    {
        if (string.IsNullOrWhiteSpace(NewBookmarkName) || GetRenderingDetails == null) return;
        var viewport = _zoomService.CurrentViewport;
        var details = GetRenderingDetails();
        var entry = new BookmarkEntry
        {
            Name = NewBookmarkName.Trim(),
            FractalType = details.type,
            Plane = viewport.Plane,
            Palette = details.palette,
            Iterations = details.iterations,
            JuliaCReal = details.juliaReal,
            JuliaCImag = details.juliaImag
        };
        Bookmarks.Add(entry);
        _bookmarkService.SaveBookmarks(new List<BookmarkEntry>(Bookmarks));
        _logger.LogInformation("Saved new bookmark: {Name}", entry.Name);
        NewBookmarkName = "";
    }

    private bool CanAddBookmark() => !string.IsNullOrWhiteSpace(NewBookmarkName);

    [RelayCommand]
    private void DeleteBookmark(BookmarkEntry? bookmark)
    {
        if (bookmark == null) return;
        _logger.LogInformation("Deleting bookmark: {Name}", bookmark.Name);
        Bookmarks.Remove(bookmark);
        _bookmarkService.SaveBookmarks(new List<BookmarkEntry>(Bookmarks));
        if (SelectedBookmark == bookmark)
        {
            SelectedBookmark = null;
        }
    }

    [RelayCommand]
    private void ZoomOut()
    {
        _logger.LogInformation("Zooming out.");
        _zoomService.ZoomOut(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Reset()
    {
        _logger.LogInformation("Resetting zoom viewport.");
        _zoomService.Reset(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public void OnPointerPressed(Point position)
    {
        SelectionStart = position;
        SelectionEnd = position;
        IsSelecting = true;
    }

    public void OnPointerMoved(Point position)
    {
        if (IsSelecting)
        {
            SelectionEnd = position;
        }
    }

    public void OnPointerReleased(Point position)
    {
        if (!IsSelecting) return;
        IsSelecting = false;
        SelectionEnd = position;

        var rect = SelectionRectangle;
        if (rect.Width < 10 || rect.Height < 10) return;

        var topLeft = CoordinateMapper.PixelToComplex((int)rect.TopLeft.X, (int)rect.TopLeft.Y, _zoomService.CurrentViewport);
        var bottomRight = CoordinateMapper.PixelToComplex((int)rect.BottomRight.X, (int)rect.BottomRight.Y, _zoomService.CurrentViewport);

        var newPlane = new ComplexPlane(
            DoubleDouble.Min(topLeft.real, bottomRight.real),
            DoubleDouble.Max(topLeft.real, bottomRight.real),
            DoubleDouble.Min(topLeft.imag, bottomRight.imag),
            DoubleDouble.Max(topLeft.imag, bottomRight.imag)
        );

        _logger.LogInformation("Zooming to bounds: Re[{R1}, {R2}] Im[{I1}, {I2}]", newPlane.RealMin, newPlane.RealMax, newPlane.ImagMin, newPlane.ImagMax);
        _zoomService.ZoomTo(newPlane, ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public void OnSizeChanged(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == ViewportWidth && height == ViewportHeight)) return;

        ViewportWidth = width;
        ViewportHeight = height;

        _zoomService.ResizeCurrent(width, height);
        _logger.LogInformation("Viewport size updated to {W}x{H}", width, height);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public void StartPan(Point position)
    {
        _panStartPoint = position;
        _panStartPlane = _zoomService.CurrentViewport.Plane;
        IsPanning = true;
    }

    public void MovePan(Point position)
    {
        if (!IsPanning) return;

        double deltaX = position.X - _panStartPoint.X;
        double deltaY = position.Y - _panStartPoint.Y;

        DoubleDouble realRange = _panStartPlane.RealMax - _panStartPlane.RealMin;
        DoubleDouble imagRange = _panStartPlane.ImagMax - _panStartPlane.ImagMin;

        DoubleDouble deltaReal = realRange * deltaX / ViewportWidth;
        DoubleDouble deltaImag = imagRange * deltaY / ViewportHeight;

        var newPlane = new ComplexPlane(
            _panStartPlane.RealMin - deltaReal,
            _panStartPlane.RealMax - deltaReal,
            _panStartPlane.ImagMin + deltaImag,
            _panStartPlane.ImagMax + deltaImag
        );

        _zoomService.UpdateCurrentPlane(newPlane);

        _panDebounceTimer?.Dispose();
        _panDebounceTimer = new Timer(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => RenderRequested?.Invoke(this, EventArgs.Empty));
        }, null, PanDebounceMs, Timeout.Infinite);
    }

    public void EndPan()
    {
        IsPanning = false;
        _panDebounceTimer?.Dispose();
        _panDebounceTimer = null;
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public void OnMouseWheelZoom(Point position, double delta)
    {
        var viewport = _zoomService.CurrentViewport;
        var (cursorReal, cursorImag) = CoordinateMapper.PixelToComplex((int)position.X, (int)position.Y, viewport);

        double zoomFactor = delta > 0 ? 0.5 : 2.0;

        DoubleDouble realRange = viewport.Plane.RealMax - viewport.Plane.RealMin;
        DoubleDouble imagRange = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

        DoubleDouble newRealRange = realRange * zoomFactor;
        DoubleDouble newImagRange = imagRange * zoomFactor;

        var newPlane = new ComplexPlane(
            cursorReal - newRealRange * 0.5,
            cursorReal + newRealRange * 0.5,
            cursorImag - newImagRange * 0.5,
            cursorImag + newImagRange * 0.5
        );

        _zoomService.ZoomTo(newPlane, ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public void PanByPercent(double percentX, double percentY)
    {
        var plane = _zoomService.CurrentViewport.Plane;
        DoubleDouble realRange = plane.RealMax - plane.RealMin;
        DoubleDouble imagRange = plane.ImagMax - plane.ImagMin;

        DoubleDouble dReal = realRange * percentX;
        DoubleDouble dImag = imagRange * percentY;

        var newPlane = new ComplexPlane(
            plane.RealMin + dReal,
            plane.RealMax + dReal,
            plane.ImagMin + dImag,
            plane.ImagMax + dImag
        );

        _zoomService.UpdateCurrentPlane(newPlane);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomCentered(bool zoomIn)
    {
        var plane = _zoomService.CurrentViewport.Plane;
        double factor = zoomIn ? 0.5 : 2.0;

        DoubleDouble realRange = plane.RealMax - plane.RealMin;
        DoubleDouble imagRange = plane.ImagMax - plane.ImagMin;
        DoubleDouble centerReal = (plane.RealMin + plane.RealMax) * 0.5;
        DoubleDouble centerImag = (plane.ImagMin + plane.ImagMax) * 0.5;

        DoubleDouble newRealRange = realRange * factor;
        DoubleDouble newImagRange = imagRange * factor;

        var newPlane = new ComplexPlane(
            centerReal - newRealRange * 0.5,
            centerReal + newRealRange * 0.5,
            centerImag - newImagRange * 0.5,
            centerImag + newImagRange * 0.5
        );

        _zoomService.ZoomTo(newPlane, ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateCursorCoordinates(Point position)
    {
        var viewport = _zoomService.CurrentViewport;
        if (viewport.ImageWidth <= 0 || viewport.ImageHeight <= 0) return;

        var (re, im) = CoordinateMapper.PixelToComplex((int)position.X, (int)position.Y, viewport);
        string sign = (double)im >= 0 ? "+" : "-";
        DoubleDouble absIm = im.Abs();
        CursorCoordinatesText = $"z = {(double)re:G6} {sign} {(double)absIm:G6}i";
    }

    public void CancelSelection()
    {
        IsSelecting = false;
        SelectionStart = default;
        SelectionEnd = default;
    }

    public void UpdateCanZoomOut() => CanZoomOut = _zoomService.CanZoomOut;

    public void UpdateViewportStats(Viewport viewport, double zoomFactor)
    {
        var centerReal = (viewport.Plane.RealMin + viewport.Plane.RealMax) * 0.5;
        var centerImag = (viewport.Plane.ImagMin + viewport.Plane.ImagMax) * 0.5;
        var spanReal = viewport.Plane.RealMax - viewport.Plane.RealMin;
        var spanImag = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

        CenterCoordinatesText = $"Re: {centerReal.ToFullString()}\nIm: {centerImag.ToFullString()}";
        SpanText = $"{spanReal.ToFullString()} × {spanImag.ToFullString()}";
        ZoomText = $"{zoomFactor:N1}×";
    }
}
```

### Step 4: Implement `DiagnosticsViewModel.cs`
Create `Fractal.UI/ViewModels/DiagnosticsViewModel.cs` for logging statistics and layout state:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Fractal.Core.Models;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly ILogger<DiagnosticsViewModel> _logger;

    [ObservableProperty]
    private bool _isDiagnosticsVisible = true;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _resolutionText = "";

    [ObservableProperty]
    private string _renderTimeText = "";

    [ObservableProperty]
    private string _iterationsText = "";

    [ObservableProperty]
    private string _engineText = "";

    public DiagnosticsViewModel(ILogger<DiagnosticsViewModel> logger)
    {
        _logger = logger;
    }

    public DiagnosticsViewModel() : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<DiagnosticsViewModel>.Instance)
    {
    }

    public void UpdateStats(long elapsedMs, int iterations, string engineName, Viewport viewport, double zoomFactor)
    {
        ResolutionText = $"{viewport.ImageWidth} × {viewport.ImageHeight}";
        RenderTimeText = $"{elapsedMs} ms";
        IterationsText = $"{iterations}";
        EngineText = engineName;
        StatusText = $"{elapsedMs} ms | {iterations} iter | {zoomFactor:F1}× ({engineName})";
    }

    public void SetStatus(string status) => StatusText = status;
}
```

### Step 5: Replace Monolithic `MainViewModel.cs`
Replace the content of `Fractal.UI/ViewModels/MainViewModel.cs` with the following clean orchestrator (~120 lines):
```csharp
using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Fractal.UI.Services;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;

    public NavigationViewModel Navigation { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public RenderingViewModel Rendering { get; }

    public string[] Languages { get; } = new[] { "EN", "PL" };

    [ObservableProperty]
    private string _selectedLanguage = "EN";

    [ObservableProperty]
    private bool _isSidePanelVisible = true;

    public Action? ToggleFullscreenAction { get; set; }

    public MainViewModel(
        NavigationViewModel navigation,
        DiagnosticsViewModel diagnostics,
        RenderingViewModel rendering,
        ILogger<MainViewModel> logger)
    {
        Navigation = navigation;
        Diagnostics = diagnostics;
        Rendering = rendering;
        _logger = logger;

        // Connect Navigation to Rendering
        Navigation.RenderRequested += OnRenderRequested;
        Navigation.BookmarkSelected += OnBookmarkSelected;
        Navigation.GetRenderingDetails = () => (
            Rendering.SelectedFractalType,
            Rendering.SelectedPalette,
            Rendering.AdaptiveIterations,
            (double)Rendering.GetJuliaCReal(),
            (double)Rendering.GetJuliaCImag()
        );

        // Connect Rendering to Navigation & Diagnostics
        Rendering.RenderRequested += OnRenderRequested;
        Rendering.RenderStarted += OnRenderStarted;
        Rendering.RenderCompleted += OnRenderCompleted;
        Rendering.RenderFailed += OnRenderFailed;

        SelectedLanguage = LocalizationService.Instance.CurrentCulture.Name.StartsWith("pl", StringComparison.OrdinalIgnoreCase) ? "PL" : "EN";

        _logger.LogInformation("MainViewModel coordination set up successfully.");

        // Initial rendering request
        OnRenderRequested(this, EventArgs.Empty);
    }

    // Parametric constructor for designer support
    public MainViewModel()
    {
        Navigation = new NavigationViewModel(new ZoomService(), new BookmarkService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<NavigationViewModel>.Instance);
        Diagnostics = new DiagnosticsViewModel(Microsoft.Extensions.Logging.Abstractions.NullLogger<DiagnosticsViewModel>.Instance);
        Rendering = new RenderingViewModel(new ParallelFractalGenerator(), new ZoomService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<RenderingViewModel>.Instance);
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MainViewModel>.Instance;
    }

    private void OnRenderRequested(object? sender, EventArgs e)
    {
        _ = Rendering.GenerateFractalAsync();
    }

    private void OnRenderStarted(object? sender, EventArgs e)
    {
        Diagnostics.SetStatus("Generating...");
    }

    private void OnRenderCompleted(object? sender, RenderCompletedEventArgs e)
    {
        if (e.ElapsedMilliseconds > 0)
        {
            Diagnostics.UpdateStats(e.ElapsedMilliseconds, e.Iterations, e.EngineName, e.Viewport, e.ZoomFactor);
            Navigation.UpdateViewportStats(e.Viewport, e.ZoomFactor);
        }
        else if (e.ElapsedMilliseconds == -1) // Image saved
        {
            Diagnostics.SetStatus($"Saved to {System.IO.Path.GetFileName(e.EngineName)}");
        }
        else if (e.ElapsedMilliseconds == -2) // Copied to clipboard
        {
            Diagnostics.SetStatus("Image copied to clipboard");
        }
    }

    private void OnRenderFailed(object? sender, RenderFailedEventArgs e)
    {
        Diagnostics.SetStatus($"Error: {e.ErrorMessage}");
    }

    private void OnBookmarkSelected(object? sender, BookmarkSelectedEventArgs e)
    {
        Rendering.SelectedFractalType = e.Bookmark.FractalType;
        Rendering.SelectedPalette = e.Bookmark.Palette;
        Rendering.AdaptiveIterations = e.Bookmark.Iterations;

        if (e.Bookmark.FractalType == FractalType.Julia)
        {
            Rendering.JuliaReal = e.Bookmark.JuliaCReal.ToString(CultureInfo.InvariantCulture);
            Rendering.JuliaImag = e.Bookmark.JuliaCImag.ToString(CultureInfo.InvariantCulture);
        }
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        _logger.LogInformation("Language updated to {Language}", value);
        LocalizationService.Instance.CurrentCulture = value == "PL"
            ? new CultureInfo("pl")
            : new CultureInfo("en");
    }

    [RelayCommand]
    private void ToggleSidePanel()
    {
        IsSidePanelVisible = !IsSidePanelVisible;
    }
}
```

### Step 6: Update View Bindings in `MainWindow.axaml`
Replace direct property paths with sub-ViewModel paths:
- Image Source: `{Binding FractalImage}` $\to$ `{Binding Rendering.FractalImage}`
- Image Width: `{Binding ViewportWidth}` $\to$ `{Binding Navigation.ViewportWidth}`
- Image Height: `{Binding ViewportHeight}` $\to$ `{Binding Navigation.ViewportHeight}`
- Selection Rect overlay: `{Binding IsSelecting}` $\to$ `{Binding Navigation.IsSelecting}`
- Selection Rect coordinates: `{Binding SelectionRectangle.X}` $\to$ `{Binding Navigation.SelectionRectangle.X}` (same for Y, Width, and Height)
- Diagnostics Panel border visibility: `{Binding IsDiagnosticsVisible}` $\to$ `{Binding Diagnostics.IsDiagnosticsVisible}`
- Diagnostic TextBlocks:
  - `{Binding ZoomText}` $\to$ `{Binding Navigation.ZoomText}`
  - `{Binding EngineText}` $\to$ `{Binding Diagnostics.EngineText}`
  - `{Binding IterationsText}` $\to$ `{Binding Diagnostics.IterationsText}`
  - `{Binding RenderTimeText}` $\to$ `{Binding Diagnostics.RenderTimeText}`
  - `{Binding ResolutionText}` $\to$ `{Binding Diagnostics.ResolutionText}`
  - `{Binding SpanText}` $\to$ `{Binding Navigation.SpanText}`
  - `{Binding CenterCoordinatesText}` $\to$ `{Binding Navigation.CenterCoordinatesText}`
- Sidebar elements:
  - Diagnostic CheckBox: `{Binding IsDiagnosticsVisible}` $\to$ `{Binding Diagnostics.IsDiagnosticsVisible}`
  - Fractal Select Box: `{Binding SelectedFractalType}` $\to$ `{Binding Rendering.SelectedFractalType}`
  - Julia Visibility: `{Binding IsJuliaSettingsVisible}` $\to$ `{Binding Rendering.IsJuliaSettingsVisible}`
  - Julia real/imag textboxes: `{Binding JuliaReal}` $\to$ `{Binding Rendering.JuliaReal}`, `{Binding JuliaImag}` $\to$ `{Binding Rendering.JuliaImag}`
  - Coloring Palette Select Box: `{Binding SelectedPalette}` $\to$ `{Binding Rendering.SelectedPalette}`
  - Bookmarks ListBox: `{Binding Bookmarks}` $\to$ `{Binding Navigation.Bookmarks}`, `{Binding SelectedBookmark}` $\to$ `{Binding Navigation.SelectedBookmark}`
  - Delete Bookmark Command: `{Binding $parent[Window].((vm:MainViewModel)DataContext).DeleteBookmarkCommand}` $\to$ `{Binding $parent[Window].((vm:MainViewModel)DataContext).Navigation.DeleteBookmarkCommand}`
  - Add Bookmark Form: `{Binding NewBookmarkName}` $\to$ `{Binding Navigation.NewBookmarkName}`, `{Binding AddBookmarkCommand}` $\to$ `{Binding Navigation.AddBookmarkCommand}`
- Control Buttons:
  - Animation Button: `{Binding ToggleAnimationCommand}` $\to$ `{Binding Rendering.ToggleAnimationCommand}`
  - Animation Button visibility: `{Binding !IsAnimating}` $\to$ `{Binding !Rendering.IsAnimating}`
  - Zoom Out Button: `{Binding ZoomOutCommand}` $\to$ `{Binding Navigation.ZoomOutCommand}`, `{Binding CanZoomOut}` $\to$ `{Binding Navigation.CanZoomOut}`
  - Reset Button: `{Binding ResetCommand}` $\to$ `{Binding Navigation.ResetCommand}`
  - Save Image Button: `{Binding SaveImageCommand}` $\to$ `{Binding Rendering.SaveImageCommand}`
  - Copy to Clipboard Button: `{Binding CopyToClipboardCommand}` $\to$ `{Binding Rendering.CopyToClipboardCommand}`
- Bottom Panel:
  - Cursor Coordinates: `{Binding CursorCoordinatesText}` $\to$ `{Binding Navigation.CursorCoordinatesText}`
  - Diagnostics status message: `{Binding StatusText}` $\to$ `{Binding Diagnostics.StatusText}`

### Step 7: Update `MainWindow.axaml.cs` Code-Behind
Replace data-context assignments and event invocations with their sub-ViewModel targets:
- Delegates inside `OnDataContextChanged`:
  - `vm.SaveFileDialogAction` $\to$ `vm.Rendering.SaveFileDialogAction`
  - `vm.CopyToClipboardAction` $\to$ `vm.Rendering.CopyToClipboardAction`
- Canvas Pointer Event Handlers:
  - `vm.OnPointerPressed(point.Position)` $\to$ `vm.Navigation.OnPointerPressed(point.Position)`
  - `vm.StartPan(point.Position)` $\to$ `vm.Navigation.StartPan(point.Position)`
  - `vm.ZoomOutCommand.Execute(...)` $\to$ `vm.Navigation.ZoomOutCommand.Execute(...)`
  - `vm.IsPanning` $\to$ `vm.Navigation.IsPanning`
  - `vm.MovePan(point.Position)` $\to$ `vm.Navigation.MovePan(point.Position)`
  - `vm.IsSelecting` $\to$ `vm.Navigation.IsSelecting`
  - `vm.OnPointerMoved(point.Position)` $\to$ `vm.Navigation.OnPointerMoved(point.Position)`
  - `vm.UpdateCursorCoordinates(point.Position)` $\to$ `vm.Navigation.UpdateCursorCoordinates(point.Position)`
  - `vm.EndPan()` $\to$ `vm.Navigation.EndPan()`
  - `vm.OnPointerReleased(point.Position)` $\to$ `vm.Navigation.OnPointerReleased(point.Position)`
  - `vm.OnSizeChanged(w, h)` $\to$ `vm.Navigation.OnSizeChanged(w, h)`
  - `vm.OnMouseWheelZoom(...)` $\to$ `vm.Navigation.OnMouseWheelZoom(...)`
- KeyDown Handler (`Window_KeyDown`):
  - Update `PanByPercent`, `ZoomCentered`, `ResetCommand`, `IsSelecting`, `CancelSelection` to use `vm.Navigation`.
  - Update `IsDiagnosticsVisible` to use `vm.Diagnostics`.
  - Update `SelectedPalette`, `CopyToClipboardCommand`, `SaveImageCommand` to use `vm.Rendering`.

### Step 8: Refactor Unit Tests (`Fractal.Tests/UI/MainViewModelTests.cs`)
Split existing tests to run against target sub-ViewModels:
```csharp
using Avalonia;
using FluentAssertions;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Fractal.UI.ViewModels;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fractal.Tests.UI;

public class MainViewModelTests
{
    private static Mock<IFractalGenerator> CreateMockGenerator()
    {
        var mock = new Mock<IFractalGenerator>();
        mock.Setup(g => g.Name).Returns("Test Generator");
        mock.Setup(g => g.IsGpuAccelerated).Returns(false);
        mock.Setup(g => g.GenerateAsync(
                It.IsAny<Viewport>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<FractalSettings>(), It.IsAny<CancellationToken>()))
            .Returns<Viewport, int, int, FractalSettings, CancellationToken>(
                (v, _, _, _, _) => Task.FromResult(new byte[v.ImageWidth * v.ImageHeight * 4]));
        return mock;
    }

    [Fact]
    public void Selection_UpdatesRectangle()
    {
        // Arrange
        var mockZoomService = new Mock<IZoomService>();
        mockZoomService.Setup(z => z.CurrentViewport).Returns(new Viewport(new ComplexPlane(0, 1, 0, 1), 800, 600));

        var navVm = new NavigationViewModel(mockZoomService.Object, new BookmarkService(), NullLogger<NavigationViewModel>.Instance);

        // Act
        navVm.OnPointerPressed(new Point(10, 10));
        navVm.OnPointerMoved(new Point(50, 50));

        // Assert
        navVm.IsSelecting.Should().BeTrue();
        navVm.SelectionRectangle.Width.Should().Be(40);
        navVm.SelectionRectangle.Height.Should().Be(40);
        navVm.SelectionRectangle.X.Should().Be(10);
        navVm.SelectionRectangle.Y.Should().Be(10);
    }

    [Fact]
    public void PointerReleased_ShouldCallZoomService()
    {
        // Arrange
        var mockZoomService = new Mock<IZoomService>();
        mockZoomService.Setup(z => z.CurrentViewport).Returns(new Viewport(new ComplexPlane(0, 100, 0, 100), 100, 100));

        var navVm = new NavigationViewModel(mockZoomService.Object, new BookmarkService(), NullLogger<NavigationViewModel>.Instance);
        navVm.ViewportWidth = 100;
        navVm.ViewportHeight = 100;

        navVm.OnPointerPressed(new Point(10, 10));
        navVm.OnPointerMoved(new Point(50, 50));

        // Act
        navVm.OnPointerReleased(new Point(50, 50));

        // Assert
        mockZoomService.Verify(z => z.ZoomTo(It.IsAny<ComplexPlane>(), 100, 100), Times.Once);
        navVm.IsSelecting.Should().BeFalse();
    }

    [Fact]
    public void OnSizeChanged_ShouldCallResizeCurrentInsteadOfZoomTo()
    {
        // Arrange
        var mockZoomService = new Mock<IZoomService>();
        mockZoomService.Setup(z => z.CurrentViewport).Returns(new Viewport(new ComplexPlane(-2, 1, -1, 1), 800, 600));

        var navVm = new NavigationViewModel(mockZoomService.Object, new BookmarkService(), NullLogger<NavigationViewModel>.Instance);

        // Act
        navVm.OnSizeChanged(1024, 768);

        // Assert
        mockZoomService.Verify(z => z.ResizeCurrent(1024, 768), Times.Once);
        mockZoomService.Verify(z => z.ZoomTo(It.IsAny<ComplexPlane>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
```

---

## 5. Verification Method

To verify the refactoring independently:

1. **Compile**:
   - Run `dotnet build` from `c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer`. Ensure it compiles successfully with zero errors.
2. **Test Run**:
   - Run `dotnet test` from `c:\Users\Admin\source\repos\Zuk\MandelbrotExplorer`. Ensure all 34 existing tests (and any updated/added UI ViewModel tests) pass cleanly.
3. **Inspect Lines Count**:
   - Verify that the refactored `MainViewModel.cs` is under 300 lines of code.
4. **Invalidation conditions**:
   - Compilation failures due to mismatched namespace declarations, missing parameterless designer constructors, or unresolved DI bindings.
   - Broken binding pathways in `MainWindow.axaml` throwing Avalonia XAML compilation errors.
