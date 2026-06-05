using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Fractal.UI.Services;

namespace Fractal.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFractalGenerator _gpuGenerator;
    private readonly IFractalGenerator _cpuGenerator = new ParallelFractalGenerator();
    private readonly IZoomService _zoomService;
    private readonly BookmarkService _bookmarkService;
    private CancellationTokenSource? _cts;

    // Pan debounce timer — prevents re-rendering on every mouse move during drag
    private Timer? _panDebounceTimer;
    private const int PanDebounceMs = 50;

    // Adaptive iteration budget — targets ~100ms render time
    private const double TargetRenderMs = 100.0;
    private const int MinIterations = 200;
    private const int MaxIterations = 50_000;
    private int _adaptiveIterations = 500;

    // Buffer reuse — avoids allocating new arrays/bitmaps when dimensions haven't changed
    private byte[]? _pixelBuffer;
    private WriteableBitmap? _reusableBitmap;
    private int _lastWidth, _lastHeight;

    [ObservableProperty]
    private WriteableBitmap? _fractalImage;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _cursorCoordinatesText = "";

    [ObservableProperty]
    private bool _isDiagnosticsVisible = true;

    [ObservableProperty]
    private string _centerCoordinatesText = "";

    [ObservableProperty]
    private string _spanText = "";

    [ObservableProperty]
    private string _resolutionText = "";

    [ObservableProperty]
    private string _renderTimeText = "";

    [ObservableProperty]
    private string _iterationsText = "";

    [ObservableProperty]
    private string _engineText = "";

    [ObservableProperty]
    private string _zoomText = "";

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
    private bool _isPanning;

    private Point _panStartPoint;
    private ComplexPlane _panStartPlane;

    [ObservableProperty]
    private bool _isSelecting;

    [ObservableProperty]
    private Point _selectionStart;

    [ObservableProperty]
    private Point _selectionEnd;

    [ObservableProperty]
    private bool _canZoomOut;

    /// <summary>
    /// Delegate set from code-behind to copy the current FractalImage to clipboard.
    /// </summary>
    public Func<Task>? CopyToClipboardAction { get; set; }

    /// <summary>
    /// Delegate set from code-behind to show a save-file dialog and return the chosen path.
    /// Returns null if the user cancelled.
    /// </summary>
    public Func<Task<string?>>? SaveFileDialogAction { get; set; }

    /// <summary>
    /// Delegate set from code-behind to toggle the window's fullscreen state.
    /// </summary>
    public Action? ToggleFullscreenAction { get; set; }

    // Viewport dimensions, bound to the Canvas/Image size
    [ObservableProperty]
    private int _viewportWidth = 800;

    [ObservableProperty]
    private int _viewportHeight = 600;

    public ObservableCollection<BookmarkEntry> Bookmarks { get; }

    [ObservableProperty]
    private BookmarkEntry? _selectedBookmark;

    [ObservableProperty]
    private string _newBookmarkName = "";

    public string[] Languages { get; } = new[] { "EN", "PL" };

    [ObservableProperty]
    private string _selectedLanguage = "EN";

    [ObservableProperty]
    private bool _isSidePanelVisible = true;

    public Rect SelectionRectangle => new Rect(
        Math.Min(SelectionStart.X, SelectionEnd.X),
        Math.Min(SelectionStart.Y, SelectionEnd.Y),
        Math.Abs(SelectionEnd.X - SelectionStart.X),
        Math.Abs(SelectionEnd.Y - SelectionStart.Y)
    );

    public MainViewModel(IFractalGenerator fractalGenerator, IZoomService zoomService, BookmarkService bookmarkService)
    {
        _gpuGenerator = fractalGenerator;
        _zoomService = zoomService;
        _bookmarkService = bookmarkService;
        _zoomService.Reset(ViewportWidth, ViewportHeight);

        // Load Bookmarks and select language
        Bookmarks = new ObservableCollection<BookmarkEntry>(_bookmarkService.LoadBookmarks());
        _selectedLanguage = LocalizationService.Instance.CurrentCulture.Name.StartsWith("pl", StringComparison.OrdinalIgnoreCase) ? "PL" : "EN";

        UpdateCanZoomOut();
        RequestRender();
    }

    // Default constructor for designer
    public MainViewModel()
    {
        _gpuGenerator = new ParallelFractalGenerator();
        _zoomService = new ZoomService();
        _bookmarkService = new BookmarkService();
        _zoomService.Reset(ViewportWidth, ViewportHeight);

        Bookmarks = new ObservableCollection<BookmarkEntry>(_bookmarkService.LoadBookmarks());
    }

    partial void OnSelectionStartChanged(Point value)
    {
        OnPropertyChanged(nameof(SelectionRectangle));
    }

    partial void OnSelectionEndChanged(Point value)
    {
        OnPropertyChanged(nameof(SelectionRectangle));
    }

    partial void OnSelectedPaletteChanged(PaletteType value)
    {
        RequestRender();
    }

    partial void OnSelectedFractalTypeChanged(FractalType value)
    {
        IsJuliaSettingsVisible = value == FractalType.Julia;
        RequestRender();
    }

    partial void OnJuliaRealChanged(string value)
    {
        RequestRender();
    }

    partial void OnJuliaImagChanged(string value)
    {
        RequestRender();
    }

    partial void OnSelectedBookmarkChanged(BookmarkEntry? value)
    {
        if (value == null) return;
        
        SelectedFractalType = value.FractalType;
        SelectedPalette = value.Palette;
        _adaptiveIterations = value.Iterations;
        
        if (value.FractalType == FractalType.Julia)
        {
            JuliaReal = value.JuliaCReal.ToString(CultureInfo.InvariantCulture);
            JuliaImag = value.JuliaCImag.ToString(CultureInfo.InvariantCulture);
        }
        
        _zoomService.ZoomTo(value.Plane, ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RequestRender();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        LocalizationService.Instance.CurrentCulture = value == "PL" 
            ? new CultureInfo("pl") 
            : new CultureInfo("en");
    }

    partial void OnNewBookmarkNameChanged(string value)
    {
        AddBookmarkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAddBookmark))]
    private void AddBookmark()
    {
        if (string.IsNullOrWhiteSpace(NewBookmarkName)) return;
        var viewport = _zoomService.CurrentViewport;
        var entry = new BookmarkEntry
        {
            Name = NewBookmarkName.Trim(),
            FractalType = SelectedFractalType,
            Plane = viewport.Plane,
            Palette = SelectedPalette,
            Iterations = _adaptiveIterations,
            JuliaCReal = (double)GetJuliaCReal(),
            JuliaCImag = (double)GetJuliaCImag()
        };
        Bookmarks.Add(entry);
        _bookmarkService.SaveBookmarks(new List<BookmarkEntry>(Bookmarks));
        NewBookmarkName = "";
    }

    private bool CanAddBookmark() => !string.IsNullOrWhiteSpace(NewBookmarkName);

    [RelayCommand]
    private void DeleteBookmark(BookmarkEntry? bookmark)
    {
        if (bookmark == null) return;
        Bookmarks.Remove(bookmark);
        _bookmarkService.SaveBookmarks(new List<BookmarkEntry>(Bookmarks));
        if (SelectedBookmark == bookmark)
        {
            SelectedBookmark = null;
        }
    }

    private DoubleDouble GetJuliaCReal()
    {
        if (double.TryParse(JuliaReal, out double val))
            return val;
        return -0.7;
    }

    private DoubleDouble GetJuliaCImag()
    {
        if (double.TryParse(JuliaImag, out double val))
            return val;
        return 0.27015;
    }

    [RelayCommand]
    private async Task GenerateFractalAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        StatusText = "Generating...";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var viewport = _zoomService.CurrentViewport;
            int iterations = _adaptiveIterations;

            double zoomFactor = 3.5 / (double)(viewport.Plane.RealMax - viewport.Plane.RealMin);

            // CPU is used at deep zoom levels (> 10^10) because GPU drivers optimize away double-double precision math
            var activeGenerator = (zoomFactor > 1e10 && _gpuGenerator.IsGpuAccelerated)
                ? _cpuGenerator
                : _gpuGenerator;

            int paletteId = (int)SelectedPalette;

            var settings = new FractalSettings(
                SelectedFractalType,
                GetJuliaCReal(),
                GetJuliaCImag()
            );

            // Reuse pixel buffer if dimensions haven't changed
            int requiredBytes = viewport.ImageWidth * viewport.ImageHeight * 4;
            if (_pixelBuffer == null || _pixelBuffer.Length != requiredBytes)
                _pixelBuffer = new byte[requiredBytes];

            byte[] pixelData = await activeGenerator.GenerateAsync(viewport, iterations, paletteId, settings, token);
            stopwatch.Stop();

            if (!token.IsCancellationRequested)
            {
                // Reuse WriteableBitmap if dimensions haven't changed
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

                // Adaptive iteration adjustment — target ~100ms render time
                double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                if (elapsedMs > 0)
                {
                    double ratio = TargetRenderMs / elapsedMs;
                    // Dampen adjustment (blend 50% old, 50% new) to avoid oscillation
                    int proposed = (int)(iterations * ratio);
                    _adaptiveIterations = Math.Clamp(
                        (iterations + proposed) / 2,
                        MinIterations,
                        MaxIterations);
                }

                var centerReal = (viewport.Plane.RealMin + viewport.Plane.RealMax) * 0.5;
                var centerImag = (viewport.Plane.ImagMin + viewport.Plane.ImagMax) * 0.5;
                var spanReal = viewport.Plane.RealMax - viewport.Plane.RealMin;
                var spanImag = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

                CenterCoordinatesText = $"Re: {centerReal.ToFullString()}\nIm: {centerImag.ToFullString()}";
                SpanText = $"{spanReal.ToFullString()} × {spanImag.ToFullString()}";
                ResolutionText = $"{viewport.ImageWidth} × {viewport.ImageHeight}";
                RenderTimeText = $"{stopwatch.ElapsedMilliseconds} ms";
                IterationsText = $"{iterations}";
                EngineText = $"{activeGenerator.Name}";
                ZoomText = $"{zoomFactor:N1}×";

                StatusText = $"{stopwatch.ElapsedMilliseconds} ms | {iterations} iter | {zoomFactor:F1}× ({activeGenerator.Name})";
            }
        }
        catch (OperationCanceledException)
        {
            // Ignored
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ZoomOut()
    {
        _zoomService.ZoomOut(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RequestRender();
    }

    [RelayCommand]
    private void Reset()
    {
        _zoomService.Reset(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RequestRender();
    }

    [RelayCommand]
    private void ToggleSidePanel()
    {
        IsSidePanelVisible = !IsSidePanelVisible;
    }

    [ObservableProperty]
    private bool _isAnimating;

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

            // Zoom in by 3% per frame
            double factor = 0.97;
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

            await GenerateFractalAsync();
        }
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
        if (rect.Width < 10 || rect.Height < 10)
        {
            // Ignore tiny selections
            return;
        }

        // Map selection rectangle to Complex Plane
        var topLeft = CoordinateMapper.PixelToComplex((int)rect.TopLeft.X, (int)rect.TopLeft.Y, _zoomService.CurrentViewport);
        var bottomRight = CoordinateMapper.PixelToComplex((int)rect.BottomRight.X, (int)rect.BottomRight.Y, _zoomService.CurrentViewport);

        // Remember Y is inverted in complex plane vs screen coordinates
        var newPlane = new ComplexPlane(
            DoubleDouble.Min(topLeft.real, bottomRight.real),
            DoubleDouble.Max(topLeft.real, bottomRight.real),
            DoubleDouble.Min(topLeft.imag, bottomRight.imag), // lower imag value
            DoubleDouble.Max(topLeft.imag, bottomRight.imag)  // higher imag value
        );

        _zoomService.ZoomTo(newPlane, ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RequestRender();
    }

    public void OnSizeChanged(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == ViewportWidth && height == ViewportHeight)) return;

        ViewportWidth = width;
        ViewportHeight = height;

        _zoomService.ResizeCurrent(width, height);
        RequestRender();
    }

    private void UpdateCanZoomOut()
    {
        CanZoomOut = _zoomService.CanZoomOut;
    }

    [RelayCommand]
    private async Task SaveImageAsync()
    {
        if (FractalImage == null) return;

        try
        {
            // If a file-picker delegate is available, use it; otherwise fall back to auto-save
            string? filePath = null;
            if (SaveFileDialogAction != null)
            {
                filePath = await SaveFileDialogAction();
            }

            if (string.IsNullOrEmpty(filePath))
            {
                // Fallback: auto-save to SavedImages folder
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SavedImages");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string fileName = $"{SelectedFractalType.ToString().Replace(" ", "")}_Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                filePath = Path.Combine(folderPath, fileName);
            }

            FractalImage.Save(filePath);
            StatusText = $"Saved to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        if (FractalImage == null || CopyToClipboardAction == null) return;

        try
        {
            await CopyToClipboardAction();
            StatusText = "Image copied to clipboard";
        }
        catch (Exception ex)
        {
            StatusText = $"Clipboard error: {ex.Message}";
        }
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

        // Debounce: restart a 50ms timer instead of rendering on every move
        _panDebounceTimer?.Dispose();
        _panDebounceTimer = new Timer(_ =>
        {
            // Timer fires on a thread-pool thread; dispatch to UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() => RequestRender());
        }, null, PanDebounceMs, Timeout.Infinite);
    }

    public void EndPan()
    {
        IsPanning = false;
        // Cancel any pending debounce timer and do one final render
        _panDebounceTimer?.Dispose();
        _panDebounceTimer = null;
        RequestRender();
    }

    /// <summary>
    /// Handles mouse wheel zoom centered on the cursor position.
    /// </summary>
    public void OnMouseWheelZoom(Point position, double delta)
    {
        var viewport = _zoomService.CurrentViewport;
        var (cursorReal, cursorImag) = CoordinateMapper.PixelToComplex((int)position.X, (int)position.Y, viewport);

        // delta > 0 = zoom in (2x), delta < 0 = zoom out (0.5x)
        double zoomFactor = delta > 0 ? 0.5 : 2.0;

        DoubleDouble realRange = viewport.Plane.RealMax - viewport.Plane.RealMin;
        DoubleDouble imagRange = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

        DoubleDouble newRealRange = realRange * zoomFactor;
        DoubleDouble newImagRange = imagRange * zoomFactor;

        // Center the new range on the cursor position
        var newPlane = new ComplexPlane(
            cursorReal - newRealRange * 0.5,
            cursorReal + newRealRange * 0.5,
            cursorImag - newImagRange * 0.5,
            cursorImag + newImagRange * 0.5
        );

        _zoomService.ZoomTo(newPlane, ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        RequestRender();
    }

    /// <summary>
    /// Pans the view by a percentage of the current viewport span.
    /// </summary>
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
        RequestRender();
    }

    /// <summary>
    /// Zooms in or out centered on the middle of the current viewport.
    /// </summary>
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
        RequestRender();
    }

    /// <summary>
    /// Updates the cursor coordinates text from a pixel position.
    /// </summary>
    public void UpdateCursorCoordinates(Point position)
    {
        var viewport = _zoomService.CurrentViewport;
        if (viewport.ImageWidth <= 0 || viewport.ImageHeight <= 0) return;

        var (re, im) = CoordinateMapper.PixelToComplex((int)position.X, (int)position.Y, viewport);
        string sign = (double)im >= 0 ? "+" : "-";
        DoubleDouble absIm = im.Abs();
        CursorCoordinatesText = $"z = {(double)re:G6} {sign} {(double)absIm:G6}i";
    }

    /// <summary>
    /// Cancels the current selection rectangle without zooming.
    /// </summary>
    public void CancelSelection()
    {
        IsSelecting = false;
        SelectionStart = default;
        SelectionEnd = default;
    }

    private void RequestRender()
    {
        TaskScheduler scheduler;
        try
        {
            scheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }
        catch
        {
            scheduler = TaskScheduler.Default;
        }

        _ = GenerateFractalAsync().ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                StatusText = $"Error: {t.Exception.InnerException?.Message}";
            }
        }, scheduler);
    }
}

