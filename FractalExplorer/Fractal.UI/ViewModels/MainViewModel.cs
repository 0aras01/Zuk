using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Fractal.UI.Services;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel>? _logger;

    public NavigationViewModel Navigation { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public RenderingViewModel Rendering { get; }

    [ObservableProperty]
    private string _selectedLanguage = "EN";

    [ObservableProperty]
    private bool _isSidePanelVisible = true;

    public string[] Languages { get; } = new[] { "EN", "PL" };

    public Func<Task>? CopyToClipboardAction { get; set; }
    public Func<string, Task>? CopyTextToClipboardAction { get; set; }
    public Func<Task<string?>>? SaveFileDialogAction { get; set; }
    public Action? ToggleFullscreenAction { get; set; }

    // Forwarding properties for compatibility with tests and views
    public WriteableBitmap? FractalImage
    {
        get => Rendering.FractalImage;
        set => Rendering.FractalImage = value;
    }

    public string StatusText
    {
        get => Rendering.StatusText;
        set => Rendering.StatusText = value;
    }

    public string CursorCoordinatesText
    {
        get => Navigation.CursorCoordinatesText;
        set => Navigation.CursorCoordinatesText = value;
    }

    public bool IsDiagnosticsVisible
    {
        get => Diagnostics.IsDiagnosticsVisible;
        set => Diagnostics.IsDiagnosticsVisible = value;
    }

    public string CenterCoordinatesText
    {
        get => Diagnostics.CenterCoordinatesText;
        set => Diagnostics.CenterCoordinatesText = value;
    }

    public string SpanText
    {
        get => Diagnostics.SpanText;
        set => Diagnostics.SpanText = value;
    }

    public string ResolutionText
    {
        get => Diagnostics.ResolutionText;
        set => Diagnostics.ResolutionText = value;
    }

    public string RenderTimeText
    {
        get => Diagnostics.RenderTimeText;
        set => Diagnostics.RenderTimeText = value;
    }

    public string IterationsText
    {
        get => Diagnostics.IterationsText;
        set => Diagnostics.IterationsText = value;
    }

    public string EngineText
    {
        get => Diagnostics.EngineText;
        set => Diagnostics.EngineText = value;
    }

    public string ZoomText
    {
        get => Diagnostics.ZoomText;
        set => Diagnostics.ZoomText = value;
    }

    public GradientPalette? SelectedPalette
    {
        get => Rendering.SelectedPalette;
        set => Rendering.SelectedPalette = value;
    }

    public ObservableCollection<GradientPalette> Palettes => Rendering.Palettes;
    public FractalType[] FractalTypes => Rendering.FractalTypes;

    public FractalType SelectedFractalType
    {
        get => Rendering.SelectedFractalType;
        set => Rendering.SelectedFractalType = value;
    }

    public string JuliaReal
    {
        get => Rendering.JuliaReal;
        set => Rendering.JuliaReal = value;
    }

    public string JuliaImag
    {
        get => Rendering.JuliaImag;
        set => Rendering.JuliaImag = value;
    }

    public bool IsJuliaSettingsVisible
    {
        get => Rendering.IsJuliaSettingsVisible;
        set => Rendering.IsJuliaSettingsVisible = value;
    }

    public bool IsPanning
    {
        get => Navigation.IsPanning;
        set => Navigation.IsPanning = value;
    }

    public bool IsSelecting
    {
        get => Navigation.IsSelecting;
        set => Navigation.IsSelecting = value;
    }

    public Point SelectionStart
    {
        get => Navigation.SelectionStart;
        set => Navigation.SelectionStart = value;
    }

    public Point SelectionEnd
    {
        get => Navigation.SelectionEnd;
        set => Navigation.SelectionEnd = value;
    }

    public bool CanZoomOut
    {
        get => Navigation.CanZoomOut;
        set => Navigation.CanZoomOut = value;
    }

    public int ViewportWidth
    {
        get => Navigation.ViewportWidth;
        set => Navigation.ViewportWidth = value;
    }

    public int ViewportHeight
    {
        get => Navigation.ViewportHeight;
        set => Navigation.ViewportHeight = value;
    }

    public ObservableCollection<BookmarkEntry> Bookmarks => Navigation.Bookmarks;

    public BookmarkEntry? SelectedBookmark
    {
        get => Navigation.SelectedBookmark;
        set {
            Navigation.SelectedBookmark = value;
            _logger?.LogInformation("Bookmark selected: {Bookmark}", value?.Name);
        }
    }

    public string NewBookmarkName
    {
        get => Navigation.NewBookmarkName;
        set => Navigation.NewBookmarkName = value;
    }

    public bool IsAnimating
    {
        get => Rendering.IsAnimating;
        set => Rendering.IsAnimating = value;
    }

    public bool IsCancelOverlayVisible
    {
        get => Rendering.IsCancelOverlayVisible;
        set => Rendering.IsCancelOverlayVisible = value;
    }

    public bool IsCancelVisible
    {
        get => Rendering.IsCancelOverlayVisible;
        set => Rendering.IsCancelOverlayVisible = value;
    }

    public Rect SelectionRectangle => Navigation.SelectionRectangle;

    // Commands forwarding
    public IAsyncRelayCommand GenerateFractalCommand => Rendering.GenerateFractalCommand;
    public IRelayCommand ToggleAnimationCommand => Rendering.ToggleAnimationCommand;
    public IAsyncRelayCommand SaveImageCommand => Rendering.SaveImageCommand;
    public IAsyncRelayCommand CopyToClipboardCommand => Rendering.CopyToClipboardCommand;
    public IRelayCommand ZoomOutCommand => Navigation.ZoomOutCommand;
    public IRelayCommand ResetCommand => Navigation.ResetCommand;
    public IRelayCommand AddBookmarkCommand => Navigation.AddBookmarkCommand;
    public IRelayCommand DeleteBookmarkCommand => Navigation.DeleteBookmarkCommand;
    public IRelayCommand CancelRenderCommand => Rendering.CancelRenderCommand;

    public MainViewModel(
        NavigationViewModel navigation,
        DiagnosticsViewModel diagnostics,
        RenderingViewModel rendering,
        ILogger<MainViewModel> logger)
    {
        _logger = logger;
        Navigation = navigation;
        Diagnostics = diagnostics;
        Rendering = rendering;

        Navigation.Main = this;
        Diagnostics.Main = this;
        Rendering.Main = this;

        _selectedLanguage = LocalizationService.Instance.CurrentCulture.Name.StartsWith("pl", StringComparison.OrdinalIgnoreCase) ? "PL" : "EN";

        Navigation.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        Diagnostics.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        Rendering.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);

        Rendering.RequestRender();
    }

    public MainViewModel()
    {
        Navigation = new NavigationViewModel();
        Diagnostics = new DiagnosticsViewModel();
        Rendering = new RenderingViewModel();

        Navigation.Main = this;
        Diagnostics.Main = this;
        Rendering.Main = this;

        Navigation.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        Diagnostics.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        Rendering.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
    }

    public MainViewModel(IFractalGenerator fractalGenerator, IZoomService zoomService, BookmarkService bookmarkService, ILogger<MainViewModel>? logger = null, ILogger<RenderingViewModel>? renderingLogger = null)
    {
        _logger = logger;
        Navigation = new NavigationViewModel(zoomService, bookmarkService, null!);
        Diagnostics = new DiagnosticsViewModel(null!);
        Rendering = new RenderingViewModel(fractalGenerator, zoomService, renderingLogger!);

        Navigation.Main = this;
        Diagnostics.Main = this;
        Rendering.Main = this;

        _selectedLanguage = LocalizationService.Instance.CurrentCulture.Name.StartsWith("pl", StringComparison.OrdinalIgnoreCase) ? "PL" : "EN";

        Navigation.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        Diagnostics.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        Rendering.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        _logger?.LogInformation("Language changed to {Language}", value);
        LocalizationService.Instance.CurrentCulture = value == "PL" 
            ? new CultureInfo("pl") 
            : new CultureInfo("en");
    }

    [RelayCommand]
    private void ToggleSidePanel()
    {
        IsSidePanelVisible = !IsSidePanelVisible;
    }

    // Methods forwarding for compatibility
    public void OnPointerPressed(Point position) => Navigation.OnPointerPressed(position);
    public void OnPointerMoved(Point position) => Navigation.OnPointerMoved(position);
    public void OnPointerReleased(Point position) => Navigation.OnPointerReleased(position);
    public void StartPan(Point position) => Navigation.StartPan(position);
    public void MovePan(Point position) => Navigation.MovePan(position);
    public void EndPan() => Navigation.EndPan();
    public void OnMouseWheelZoom(Point position, double delta) => Navigation.OnMouseWheelZoom(position, delta);
    public void PanByPercent(double percentX, double percentY) => Navigation.PanByPercent(percentX, percentY);
    public void ZoomCentered(bool zoomIn) => Navigation.ZoomCentered(zoomIn);
    public void UpdateCursorCoordinates(Point position) => Navigation.UpdateCursorCoordinates(position);
    public void CancelSelection() => Navigation.CancelSelection();
    public void OnSizeChanged(int width, int height) => Navigation.OnSizeChanged(width, height);

    [ObservableProperty]
    private bool _isColorPaletteEditorVisible;

    [RelayCommand]
    private void OpenColorPaletteEditor()
    {
        IsColorPaletteEditorVisible = true;
        Rendering.OpenPaletteEditorCommand.Execute(null);
    }

    [RelayCommand]
    private void CloseColorPaletteEditor()
    {
        IsColorPaletteEditorVisible = false;
    }

    [ObservableProperty]
    private bool _isMinimapVisible;

    [RelayCommand]
    private void ToggleMinimap()
    {
        IsMinimapVisible = !IsMinimapVisible;
    }

    [ObservableProperty]
    private WriteableBitmap? _minimapImage;

    [ObservableProperty]
    private Rect _minimapViewportRect;

    private CancellationTokenSource? _minimapCts;
    private static readonly ComplexPlane MinimapPlane = ZoomService.AdjustAspectRatio(new ComplexPlane(-2.5, 1.0, -1.5, 1.5), 160, 120);

    public async Task GenerateMinimapAsync()
    {
        _minimapCts?.Cancel();
        _minimapCts = new CancellationTokenSource();
        var token = _minimapCts.Token;

        try
        {
            var viewport = new Viewport(MinimapPlane, 160, 120);
            var palette = Rendering.SelectedPalette ?? (Rendering.Palettes.Count > 0 ? Rendering.Palettes[0] : new GradientPalette());
            var settings = new FractalSettings(
                Rendering.SelectedFractalType,
                Rendering.GetJuliaCReal(),
                Rendering.GetJuliaCImag()
            );

            var (pixels, _) = await Rendering.GpuGenerator.GenerateAsync(viewport, 100, palette, Rendering.PaletteOffset, settings, token);

            if (!token.IsCancellationRequested)
            {
                var bitmap = new WriteableBitmap(
                    new PixelSize(160, 120),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Opaque);

                using (var frameBuffer = bitmap.Lock())
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixels, 0, frameBuffer.Address, pixels.Length);
                }

                MinimapImage = bitmap;
            }
        }
        catch
        {
            // Ignore cancellation or render failures for minimap
        }
    }

    public void UpdateMinimapViewportRect()
    {
        var currentViewport = Navigation.ZoomService.CurrentViewport;
        var currentPlane = currentViewport.Plane;

        double wm = 160.0;
        double hm = 120.0;

        double rMinMinimap = (double)MinimapPlane.RealMin;
        double rMaxMinimap = (double)MinimapPlane.RealMax;
        double iMinMinimap = (double)MinimapPlane.ImagMin;
        double iMaxMinimap = (double)MinimapPlane.ImagMax;

        double rRange = rMaxMinimap - rMinMinimap;
        double iRange = iMaxMinimap - iMinMinimap;

        if (rRange == 0 || iRange == 0) return;

        double left = wm * ((double)currentPlane.RealMin - rMinMinimap) / rRange;
        double right = wm * ((double)currentPlane.RealMax - rMinMinimap) / rRange;
        double top = hm * (iMaxMinimap - (double)currentPlane.ImagMax) / iRange;
        double bottom = hm * (iMaxMinimap - (double)currentPlane.ImagMin) / iRange;

        double x = left;
        double y = top;
        double width = right - left;
        double height = bottom - top;

        // Ensure a minimum size of 2x2 for visibility at extreme zoom levels
        if (width < 2.0) width = 2.0;
        if (height < 2.0) height = 2.0;

        MinimapViewportRect = new Rect(x, y, width, height);
        RecalculateOrbitPixels();
    }

    public void OnMinimapClick(Point position)
    {
        double px = position.X;
        double py = position.Y;

        double wm = 160.0;
        double hm = 120.0;

        double rMinMinimap = (double)MinimapPlane.RealMin;
        double rMaxMinimap = (double)MinimapPlane.RealMax;
        double iMinMinimap = (double)MinimapPlane.ImagMin;
        double iMaxMinimap = (double)MinimapPlane.ImagMax;

        double rRange = rMaxMinimap - rMinMinimap;
        double iRange = iMaxMinimap - iMinMinimap;

        double clickReal = rMinMinimap + (px / wm) * rRange;
        double clickImag = iMaxMinimap - (py / hm) * iRange;

        var currentViewport = Navigation.ZoomService.CurrentViewport;
        var currentPlane = currentViewport.Plane;
        DoubleDouble currentRealRange = currentPlane.RealMax - currentPlane.RealMin;
        DoubleDouble currentImagRange = currentPlane.ImagMax - currentPlane.ImagMin;

        var newPlane = new ComplexPlane(
            clickReal - currentRealRange * 0.5,
            clickReal + currentRealRange * 0.5,
            clickImag - currentImagRange * 0.5,
            clickImag + currentImagRange * 0.5
        );

        Navigation.ZoomService.ZoomTo(newPlane, Navigation.ViewportWidth, Navigation.ViewportHeight);
        Navigation.UpdateCanZoomOut();
        Rendering.RequestRender();
        UpdateMinimapViewportRect();
    }

    [ObservableProperty]
    private bool _isOrbitPathVisible;

    [RelayCommand]
    private void ToggleOrbit()
    {
        IsOrbitPathVisible = !IsOrbitPathVisible;
        if (!IsOrbitPathVisible)
        {
            _orbitComplexPoints.Clear();
            OrbitPoints.Clear();
        }
    }

    public ObservableCollection<Point> OrbitPoints { get; } = new();
    private readonly System.Collections.Generic.List<(DoubleDouble real, DoubleDouble imag)> _orbitComplexPoints = new();

    public void CalculateOrbit(Point pixelPos)
    {
        _orbitComplexPoints.Clear();
        OrbitPoints.Clear();

        if (!IsOrbitPathVisible)
            return;

        var viewport = Navigation.ZoomService.CurrentViewport;
        var (clickReal, clickImag) = CoordinateMapper.PixelToComplex((int)pixelPos.X, (int)pixelPos.Y, viewport);

        DoubleDouble zReal;
        DoubleDouble zImag;
        DoubleDouble cReal;
        DoubleDouble cImag;

        var type = Rendering.SelectedFractalType;
        var maxIterations = Math.Min(Rendering.AdaptiveIterations, 500);

        if (type == FractalType.Julia)
        {
            zReal = clickReal;
            zImag = clickImag;
            cReal = Rendering.GetJuliaCReal();
            cImag = Rendering.GetJuliaCImag();
        }
        else
        {
            zReal = 0.0;
            zImag = 0.0;
            cReal = clickReal;
            cImag = clickImag;
        }

        _orbitComplexPoints.Add((zReal, zImag));

        int iterations = 0;
        while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
        {
            DoubleDouble tempReal;
            if (type == FractalType.BurningShip)
            {
                tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = (zReal * zImag).Abs() * 2.0 + cImag;
                zReal = tempReal;
            }
            else if (type == FractalType.Tricorn)
            {
                tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = zReal * zImag * -2.0 + cImag;
                zReal = tempReal;
            }
            else if (type == FractalType.Celtic)
            {
                tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
                zImag = zReal * zImag * 2.0 + cImag;
                zReal = tempReal;
            }
            else if (type == FractalType.Buffalo)
            {
                tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
                zImag = (zReal * zImag).Abs() * 2.0 + cImag;
                zReal = tempReal;
            }
            else if (type == FractalType.Multibrot3)
            {
                tempReal = zReal * (zReal * zReal - zImag * zImag * 3.0) + cReal;
                zImag = zImag * (zReal * zReal * 3.0 - zImag * zImag) + cImag;
                zReal = tempReal;
            }
            else // Mandelbrot & Julia
            {
                tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = zReal * zImag * 2.0 + cImag;
                zReal = tempReal;
            }

            _orbitComplexPoints.Add((zReal, zImag));
            iterations++;
        }

        RecalculateOrbitPixels();
    }

    public void RecalculateOrbitPixels()
    {
        if (!IsOrbitPathVisible || _orbitComplexPoints.Count == 0)
        {
            OrbitPoints.Clear();
            return;
        }

        var viewport = Navigation.ZoomService.CurrentViewport;
        var points = new System.Collections.Generic.List<Point>(_orbitComplexPoints.Count);
        foreach (var (r, i) in _orbitComplexPoints)
        {
            var (x, y) = CoordinateMapper.ComplexToPixel(r, i, viewport);
            points.Add(new Point(x, y));
        }

        OrbitPoints.Clear();
        foreach (var pt in points)
        {
            OrbitPoints.Add(pt);
        }
    }

    public bool Is3DShadingEnabled { get; set; }
    public IRelayCommand? Toggle3DShadingCommand { get; set; }

    public bool IsHighResExporting { get; set; }
    public IAsyncRelayCommand? StartHighResExportCommand { get; set; }

    public bool IsGifExporting { get; set; }
    public IAsyncRelayCommand? StartGifExportCommand { get; set; }

    public IRelayCommand? RandomDiscoverCommand { get; set; }

    public bool IsSplitViewEnabled { get; set; }
    public IRelayCommand? ToggleSplitViewCommand { get; set; }

    [ObservableProperty]
    private bool _isScientificNotationEnabled;

    [RelayCommand]
    private void ToggleScientificNotation()
    {
        IsScientificNotationEnabled = !IsScientificNotationEnabled;
    }

    partial void OnIsScientificNotationEnabledChanged(bool value)
    {
        UpdateCoordinatesTexts();
        Navigation.RefreshCursorCoordinatesText();
    }

    public void UpdateCoordinatesTexts()
    {
        var viewport = Navigation.ZoomService.CurrentViewport;
        var centerReal = (viewport.Plane.RealMin + viewport.Plane.RealMax) * 0.5;
        var centerImag = (viewport.Plane.ImagMin + viewport.Plane.ImagMax) * 0.5;
        var spanReal = viewport.Plane.RealMax - viewport.Plane.RealMin;
        var spanImag = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

        if (IsScientificNotationEnabled)
        {
            Diagnostics.CenterCoordinatesText = $"Re: {((double)centerReal):E10}\nIm: {((double)centerImag):E10}";
            Diagnostics.SpanText = $"{((double)spanReal):E10} × {((double)spanImag):E10}";
        }
        else
        {
            Diagnostics.CenterCoordinatesText = $"Re: {centerReal.ToFullString()}\nIm: {centerImag.ToFullString()}";
            Diagnostics.SpanText = $"{spanReal.ToFullString()} × {spanImag.ToFullString()}";
        }
    }

    [RelayCommand]
    public async Task CopyCenterCoordinatesToClipboardAsync()
    {
        var viewport = Navigation.ZoomService.CurrentViewport;
        var centerReal = (viewport.Plane.RealMin + viewport.Plane.RealMax) * 0.5;
        var centerImag = (viewport.Plane.ImagMin + viewport.Plane.ImagMax) * 0.5;
        string text = $"Re: {centerReal.ToFullString()}\nIm: {centerImag.ToFullString()}";
        if (CopyTextToClipboardAction != null)
        {
            await CopyTextToClipboardAction(text);
            StatusText = LocalizationService.Instance["StatusCoordinatesCopied"] ?? "Coordinates copied to clipboard";
        }
    }

    [RelayCommand]
    public async Task CopyCursorCoordinatesToClipboardAsync()
    {
        string text = $"Re: {Navigation.LastCursorRe.ToFullString()}\nIm: {Navigation.LastCursorIm.ToFullString()}";
        if (CopyTextToClipboardAction != null)
        {
            await CopyTextToClipboardAction(text);
            StatusText = LocalizationService.Instance["StatusCoordinatesCopied"] ?? "Coordinates copied to clipboard";
        }
    }
}
