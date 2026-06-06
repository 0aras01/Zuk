# Handoff Report: Milestone 3 — Sub-ViewModels Implementation Strategy

## 1. Observation

A read-only investigation of the `MandelbrotExplorer` codebase and the proposed architectural changes was completed. The following direct observations were made:

- **Monolith Analysis**: `Fractal.UI/ViewModels/MainViewModel.cs` is currently a 740-line monolith combining viewport navigation, diagnostic telemetry, canvas state, fractal generation, animation loops, file exports, and language/side-panel layout details.
- **Stubs Inspection**: The target sub-ViewModels (`NavigationViewModel.cs`, `DiagnosticsViewModel.cs`, and `RenderingViewModel.cs`) in `Fractal.UI/ViewModels/` are currently stub classes inheriting from `ObservableObject` with basic dependency injection (DI) constructors.
- **Dependency Injection & Logger**: In `Fractal.UI/App.axaml.cs`, logging is configured with Console and Debug outputs via `Microsoft.Extensions.Logging`. The sub-ViewModels and parent coordinator are already registered in the DI container.
- **NuGet Packages**: `Fractal.UI/Fractal.UI.csproj` already references the necessary logging NuGet packages:
  - `Microsoft.Extensions.Logging` (v10.0.8)
  - `Microsoft.Extensions.Logging.Console` (v10.0.8)
  - `Microsoft.Extensions.Logging.Debug` (v10.0.8)
- **Visual Bindings**: `MainWindow.axaml` and `MainWindow.axaml.cs` bind directly to the properties, methods, and commands of the monolithic `MainViewModel`.
- **Test Coverage**: The existing xUnit tests (`MainViewModelTests.cs` and `E2ETests.cs`) assert against properties and commands on the monolithic `MainViewModel` (e.g., `vm.ZoomText`, `vm.IsSelecting`, `vm.GenerateFractalCommand`).

---

## 2. Logic Chain

1. **Separation of Concerns (SRP)**: Slicing the monolithic view model into decoupled sub-ViewModels (`NavigationViewModel`, `DiagnosticsViewModel`, `RenderingViewModel`) prevents violation of the Single Responsibility Principle, reduces coordination clutter, and enables granular unit testing.
2. **Decoupled Coordination Pattern**: 
   - Direct dependencies between sub-ViewModels are prohibited. Instead, communication is orchestrated by the parent `MainViewModel` using C# events.
   - `NavigationViewModel` raises `RenderRequested` and `BookmarkSelected` when the viewport changes or bookmark selection occurs.
   - `RenderingViewModel` raises `RenderRequested`, `RenderStarted`, `RenderCompleted`, `RenderFailed`, `ImageSaved`, and `ImageCopiedToClipboard` events.
   - The coordinator `MainViewModel` subscribes to these events and manages the flow of data between the sub-ViewModels (e.g. updating Diagnostics stats on render completion, or updating rendering settings when a bookmark is navigated to).
3. **Structured Logging Integration**:
   - Injecting `ILogger<T>` into each view model allows capturing fine-grained user activity and rendering metrics.
   - Logging logic is embedded into critical operations: rendering requests, render completion metrics (duration in ms, engine used), exceptional flows (failed generation, failed exports), bookmark mutations, and language setting adjustments.
4. **Test Adjustments**:
   - Because the view bindings and properties are moving from the monolithic parent to the sub-ViewModels, we must update the binding paths in `MainWindow.axaml` and references in `MainWindow.axaml.cs`.
   - Similarly, the test suites in `MainViewModelTests.cs` and `E2ETests.cs` must be updated to target the sub-ViewModels directly.

---

## 3. Caveats

- **Designer Compatibility**: Avalonia's visual designer requires parameterless constructors for all ViewModels. These constructors must instantiate mock/null services (such as `NullLogger<T>.Instance`) to prevent visual designer crashes.
- **Resource Cleanup**: When generating fractals or running animations, cancellation tokens are handled inside `RenderingViewModel`. Proper thread cancellation is critical so that starting a new render cancels any active background generation immediately.

---

## 4. Conclusion & Proposed Refactoring Strategy

The refactoring will split `MainViewModel` into highly cohesive sub-ViewModels, integrate logging, configure coordination, update visual bindings, and adapt the test suite.

### Detailed Code Implementations

#### 1. `DiagnosticsViewModel.cs`
Responsible for displaying application status, performance statistics, and handling diagnostics visibility.

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

    public void SetStatus(string status)
    {
        StatusText = status;
    }
}
```

#### 2. `NavigationViewModel.cs`
Manages the viewport bounds, mouse click-drag selection, panning, zoom history, cursor coordinate mapping, and bookmarks.

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

    // Delegate to retrieve current rendering state from RenderingViewModel safely
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
        
        // Log bookmark navigation
        _logger.LogInformation("Navigating to bookmark: {Name} (FractalType={FractalType}, Iterations={Iterations}, Palette={Palette})", 
            value.Name, value.FractalType, value.Iterations, value.Palette);

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

        // Log bookmark addition
        _logger.LogInformation("Saved new bookmark: {Name} (FractalType={FractalType}, Iterations={Iterations})", entry.Name, entry.FractalType, entry.Iterations);
        
        NewBookmarkName = "";
    }

    private bool CanAddBookmark() => !string.IsNullOrWhiteSpace(NewBookmarkName);

    [RelayCommand]
    private void DeleteBookmark(BookmarkEntry? bookmark)
    {
        if (bookmark == null) return;

        // Log bookmark deletion
        _logger.LogInformation("Deleting bookmark: {Name}", bookmark.Name);

        Bookmarks.Remove(bookmark);
        _bookmarkService.SaveBookmarks(new List<BookmarkEntry>(Bookmarks));
        if (SelectedBookmark == bookmark)
        {
            SelectedBookmark = null;
        }
    }

    [RelayCommand]
    public void ZoomOut()
    {
        _logger.LogInformation("Zooming out.");
        _zoomService.ZoomOut(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Reset()
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

#### 3. `RenderingViewModel.cs`
Responsible for calculation engine selection, triggering async generations, managing animation thread execution, saving PNG files, copying bitmaps to clipboard, and implementing adaptive iteration updates.

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

    // Platform UI integrations injected from code-behind
    public Func<Task>? CopyToClipboardAction { get; set; }
    public Func<Task<string?>>? SaveFileDialogAction { get; set; }

    // Communication events handled by MainViewModel coordinator
    public event EventHandler? RenderStarted;
    public event EventHandler? RenderRequested;
    public event EventHandler<RenderCompletedEventArgs>? RenderCompleted;
    public event EventHandler<RenderFailedEventArgs>? RenderFailed;
    public event EventHandler<string>? ImageSaved;
    public event EventHandler? ImageCopiedToClipboard;

    public RenderingViewModel(IFractalGenerator gpuGenerator, IZoomService zoomService, ILogger<RenderingViewModel> logger)
    {
        _gpuGenerator = gpuGenerator;
        _zoomService = zoomService;
        _logger = logger;
    }

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

        // Requirement: Log rendering request with type/iterations
        _logger.LogInformation("Generating fractal. Type={Type}, Iterations={Iterations}", SelectedFractalType, AdaptiveIterations);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var viewport = _zoomService.CurrentViewport;
            int iterations = AdaptiveIterations;
            double zoomFactor = 3.5 / (double)(viewport.Plane.RealMax - viewport.Plane.RealMin);

            // Select rendering engine (deep zooms > 1e10 run on CPU to avoid double-precision limitations in GPU)
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

                // Adjust iterations dynamically to match render speed targets
                double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                if (elapsedMs > 0)
                {
                    double ratio = TargetRenderMs / elapsedMs;
                    int proposed = (int)(iterations * ratio);
                    AdaptiveIterations = Math.Clamp((iterations + proposed) / 2, MinIterations, MaxIterations);
                }

                // Requirement: On render completion, log duration and engine used
                _logger.LogInformation("Render completed in {ElapsedMs} ms using engine: {EngineName}", stopwatch.ElapsedMilliseconds, activeGenerator.Name);

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
            // Requirement: Log generation exceptions
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
            _logger.LogInformation("Animation toggled to Stopped.");
        }
        else
        {
            IsAnimating = true;
            _logger.LogInformation("Animation toggled to Started. Type={Type}, Iterations={Iterations}", SelectedFractalType, AdaptiveIterations);
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
        
        // Requirement: Log rendering save request with type/iterations
        _logger.LogInformation("Requested save image. Type={Type}, Iterations={Iterations}", SelectedFractalType, AdaptiveIterations);

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
            
            ImageSaved?.Invoke(this, filePath);
        }
        catch (Exception ex)
        {
            // Requirement: Log saving exception
            _logger.LogError(ex, "Failed to save image.");
            RenderFailed?.Invoke(this, new RenderFailedEventArgs($"Save error: {ex.Message}"));
        }
    }

    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        if (FractalImage == null || CopyToClipboardAction == null) return;

        // Requirement: Log rendering copy request with type/iterations
        _logger.LogInformation("Requested copy image to clipboard. Type={Type}, Iterations={Iterations}", SelectedFractalType, AdaptiveIterations);

        try
        {
            await CopyToClipboardAction();
            _logger.LogInformation("Copied image to clipboard.");
            
            ImageCopiedToClipboard?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // Requirement: Log copy exception
            _logger.LogError(ex, "Failed to copy image to clipboard.");
            RenderFailed?.Invoke(this, new RenderFailedEventArgs($"Clipboard error: {ex.Message}"));
        }
    }

    public DoubleDouble GetJuliaCReal() => double.TryParse(JuliaReal, out double val) ? val : -0.7;
    public DoubleDouble GetJuliaCImag() => double.TryParse(JuliaImag, out double val) ? val : 0.27015;
}
```

#### 4. `MainViewModel.cs`
The main coordinator of sub-ViewModels, holding languages lists and UI sidebar panel visibilities.

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

        // Bind Navigation events to render triggers
        Navigation.RenderRequested += OnRenderRequested;
        Navigation.BookmarkSelected += OnBookmarkSelected;
        Navigation.GetRenderingDetails = () => (
            Rendering.SelectedFractalType,
            Rendering.SelectedPalette,
            Rendering.AdaptiveIterations,
            (double)Rendering.GetJuliaCReal(),
            (double)Rendering.GetJuliaCImag()
        );

        // Bind Rendering events to update diagnostic displays
        Rendering.RenderRequested += OnRenderRequested;
        Rendering.RenderStarted += OnRenderStarted;
        Rendering.RenderCompleted += OnRenderCompleted;
        Rendering.RenderFailed += OnRenderFailed;
        Rendering.ImageSaved += OnImageSaved;
        Rendering.ImageCopiedToClipboard += OnImageCopiedToClipboard;

        SelectedLanguage = LocalizationService.Instance.CurrentCulture.Name.StartsWith("pl", StringComparison.OrdinalIgnoreCase) ? "PL" : "EN";

        _logger.LogInformation("MainViewModel coordination set up successfully.");

        // Request initial load render
        OnRenderRequested(this, EventArgs.Empty);
    }

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
        Diagnostics.UpdateStats(e.ElapsedMilliseconds, e.Iterations, e.EngineName, e.Viewport, e.ZoomFactor);
        Navigation.UpdateViewportStats(e.Viewport, e.ZoomFactor);
    }

    private void OnRenderFailed(object? sender, RenderFailedEventArgs e)
    {
        Diagnostics.SetStatus($"Error: {e.ErrorMessage}");
    }

    private void OnImageSaved(object? sender, string path)
    {
        Diagnostics.SetStatus($"Saved to {System.IO.Path.GetFileName(path)}");
    }

    private void OnImageCopiedToClipboard(object? sender, EventArgs e)
    {
        Diagnostics.SetStatus("Image copied to clipboard");
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
        // Requirement: Log language change setting updates
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

---

## 5. Step-by-Step Implementation Strategy

### Step 1: Replace Stub Implementations
1. Replace `Fractal.UI/ViewModels/DiagnosticsViewModel.cs` with the full implementation proposed above.
2. Replace `Fractal.UI/ViewModels/NavigationViewModel.cs` with the full implementation proposed above.
3. Replace `Fractal.UI/ViewModels/RenderingViewModel.cs` with the full implementation proposed above.
4. Replace `Fractal.UI/ViewModels/MainViewModel.cs` with the thin orchestrator class proposed above.

### Step 2: Update View Bindings in `MainWindow.axaml`
Update control paths in `Fractal.UI/Views/MainWindow.axaml` to access sub-ViewModels directly:
- **Canvas / Image**:
  - Image Source: `{Binding FractalImage}` $\to$ `{Binding Rendering.FractalImage}`
  - Image Width: `{Binding ViewportWidth}` $\to$ `{Binding Navigation.ViewportWidth}`
  - Image Height: `{Binding ViewportHeight}` $\to$ `{Binding Navigation.ViewportHeight}`
  - Selection overlay visibility: `{Binding IsSelecting}` $\to$ `{Binding Navigation.IsSelecting}`
  - Selection rectangle bounds: `{Binding SelectionRectangle.X}` $\to$ `{Binding Navigation.SelectionRectangle.X}` (similarly for Y, Width, and Height)
- **Telemetry Overlay**:
  - Border visibility: `{Binding IsDiagnosticsVisible}` $\to$ `{Binding Diagnostics.IsDiagnosticsVisible}`
  - Zoom ratio: `{Binding ZoomText}` $\to$ `{Binding Navigation.ZoomText}`
  - Engine name: `{Binding EngineText}` $\to$ `{Binding Diagnostics.EngineText}`
  - Iteration limits: `{Binding IterationsText}` $\to$ `{Binding Diagnostics.IterationsText}`
  - Elapsed render time: `{Binding RenderTimeText}` $\to$ `{Binding Diagnostics.RenderTimeText}`
  - Resolution size: `{Binding ResolutionText}` $\to$ `{Binding Diagnostics.ResolutionText}`
  - Math span: `{Binding SpanText}` $\to$ `{Binding Navigation.SpanText}`
  - Center coordinates: `{Binding CenterCoordinatesText}` $\to$ `{Binding Navigation.CenterCoordinatesText}`
- **Sidebar Control Elements**:
  - Diagnostics checkbox: `{Binding IsDiagnosticsVisible}` $\to$ `{Binding Diagnostics.IsDiagnosticsVisible}`
  - Fractal type combo: `{Binding SelectedFractalType}` $\to$ `{Binding Rendering.SelectedFractalType}`
  - Julia parameters visibility: `{Binding IsJuliaSettingsVisible}` $\to$ `{Binding Rendering.IsJuliaSettingsVisible}`
  - Julia real/imag text inputs: `{Binding JuliaReal}` $\to$ `{Binding Rendering.JuliaReal}`, `{Binding JuliaImag}` $\to$ `{Binding Rendering.JuliaImag}`
  - Palette selector combo: `{Binding SelectedPalette}` $\to$ `{Binding Rendering.SelectedPalette}`
  - Bookmarks ListBox source: `{Binding Bookmarks}` $\to$ `{Binding Navigation.Bookmarks}`
  - Selected bookmark item: `{Binding SelectedBookmark}` $\to$ `{Binding Navigation.SelectedBookmark}`
  - Delete bookmark command: `{Binding $parent[Window].((vm:MainViewModel)DataContext).DeleteBookmarkCommand}` $\to$ `{Binding $parent[Window].((vm:MainViewModel)DataContext).Navigation.DeleteBookmarkCommand}`
  - New bookmark input: `{Binding NewBookmarkName}` $\to$ `{Binding Navigation.NewBookmarkName}`
  - Add bookmark button command: `{Binding AddBookmarkCommand}` $\to$ `{Binding Navigation.AddBookmarkCommand}`
- **Action Buttons**:
  - Play animation: `{Binding ToggleAnimationCommand}` $\to$ `{Binding Rendering.ToggleAnimationCommand}`
  - Animation visibility filters: `{Binding !IsAnimating}` $\to$ `{Binding !Rendering.IsAnimating}` / `{Binding IsAnimating}` $\to$ `{Binding Rendering.IsAnimating}`
  - Zoom out: `{Binding ZoomOutCommand}` $\to$ `{Binding Navigation.ZoomOutCommand}`
  - Can zoom out check: `{Binding CanZoomOut}` $\to$ `{Binding Navigation.CanZoomOut}`
  - Reset viewport: `{Binding ResetCommand}` $\to$ `{Binding Navigation.ResetCommand}`
  - Save file button: `{Binding SaveImageCommand}` $\to$ `{Binding Rendering.SaveImageCommand}`
  - Copy button command: `{Binding CopyToClipboardCommand}` $\to$ `{Binding Rendering.CopyToClipboardCommand}`
- **Bottom Telemetry Status Bar**:
  - Cursor coordinates: `{Binding CursorCoordinatesText}` $\to$ `{Binding Navigation.CursorCoordinatesText}`
  - Status display text: `{Binding StatusText}` $\to$ `{Binding Diagnostics.StatusText}`

### Step 3: Update `MainWindow.axaml.cs` Code-Behind
Update event delegate wirings and control canvas pointer handlers:
- **Delegates in `OnDataContextChanged`**:
  - `vm.SaveFileDialogAction` $\to$ `vm.Rendering.SaveFileDialogAction`
  - `vm.CopyToClipboardAction` $\to$ `vm.Rendering.CopyToClipboardAction`
- **Pointer/Size Events**:
  - Update pointer presses/movement/released/resize to call:
    - `vm.Navigation.OnPointerPressed`
    - `vm.Navigation.StartPan`
    - `vm.Navigation.ZoomOutCommand`
    - `vm.Navigation.IsPanning`
    - `vm.Navigation.MovePan`
    - `vm.Navigation.IsSelecting`
    - `vm.Navigation.OnPointerMoved`
    - `vm.Navigation.UpdateCursorCoordinates`
    - `vm.Navigation.EndPan`
    - `vm.Navigation.OnPointerReleased`
    - `vm.Navigation.OnSizeChanged`
    - `vm.Navigation.OnMouseWheelZoom`
- **Keyboard Shortcuts (`Window_KeyDown`)**:
  - Re-route panning commands (`PanByPercent`, `ZoomCentered`, `ResetCommand`, `IsSelecting`, `CancelSelection`) to target `vm.Navigation`.
  - Re-route diagnostics command checks to target `vm.Diagnostics`.
  - Re-route rendering color adjustments and save/copy triggers to target `vm.Rendering`.

### Step 4: Refactor Test Suites
1. **`Fractal.Tests/UI/MainViewModelTests.cs`**:
   Refactor layout/mouse logic assertions to run against instances of `NavigationViewModel` directly:
   - Instantiate `NavigationViewModel` using mocks of `IZoomService`, `BookmarkService`, and `NullLogger<NavigationViewModel>.Instance`.
   - Update assertions (`IsSelecting`, `SelectionRectangle`) to test the sub-ViewModel properties directly rather than via `MainViewModel`.
2. **`Fractal.Tests/UI/E2ETests.cs`**:
   Since the E2E tests target a created `MainViewModel` and test coordinates, presets, bookmarks, and render parameters, they must be updated to inspect and trigger commands via the sub-ViewModels:
   - Querying zoom: `vm.ZoomText` $\to$ `vm.Navigation.ZoomText`
   - Invoking wheel zoom: `vm.OnMouseWheelZoom` $\to$ `vm.Navigation.OnMouseWheelZoom`
   - Checking zoom status: `vm.CanZoomOut` $\to$ `vm.Navigation.CanZoomOut`
   - Activating panning: `vm.StartPan`/`vm.MovePan`/`vm.EndPan` $\to$ `vm.Navigation.StartPan`/`vm.Navigation.MovePan`/`vm.Navigation.EndPan`
   - Drag selection zoom: `vm.OnPointerPressed`/`vm.OnPointerMoved`/`vm.OnPointerReleased` $\to$ `vm.Navigation.OnPointerPressed`/`vm.Navigation.OnPointerMoved`/`vm.Navigation.OnPointerReleased`
   - Command triggers: `vm.ResetCommand` $\to$ `vm.Navigation.ResetCommand`, `vm.AddBookmarkCommand` $\to$ `vm.Navigation.AddBookmarkCommand`, `vm.SaveImageCommand` $\to$ `vm.Rendering.SaveImageCommand`, `vm.GenerateFractalCommand.ExecuteAsync(null)` $\to$ `vm.Rendering.GenerateFractalAsync()`.

---

## 6. Verification Method

Independent verification of the refactoring can be performed with the following commands and check criteria:

1. **Compilation Check**:
   Run the build in the workspace root:
   ```powershell
   dotnet build
   ```
   Ensure compilation succeeds with zero compilation errors and warnings.

2. **Test Suite Execution**:
   Run the test runner to verify behavior:
   ```powershell
   dotnet test
   ```
   Ensure all 34 existing core tests, UI tests, and updated E2E tests pass successfully.

3. **Code Quality Metrics**:
   - `MainViewModel.cs` should be reduced from 740 lines to under 200 lines.
   - Verify that sub-ViewModels (`NavigationViewModel`, `DiagnosticsViewModel`, `RenderingViewModel`) are decoupled from each other and communicate solely via standard event handler subscriptions.
