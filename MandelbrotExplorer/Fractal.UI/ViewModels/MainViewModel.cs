using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Fractal.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFractalGenerator _gpuGenerator;
    private readonly IFractalGenerator _cpuGenerator = new ParallelFractalGenerator();
    private readonly IZoomService _zoomService;
    private CancellationTokenSource? _cts;

    // Adaptive iteration budget — targets ~100ms render time
    private const double TargetRenderMs = 100.0;
    private const int MinIterations = 200;
    private const int MaxIterations = 50_000;
    private int _adaptiveIterations = 500;

    [ObservableProperty]
    private WriteableBitmap? _fractalImage;

    [ObservableProperty]
    private string _statusText = "Ready";

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

    public List<string> Palettes { get; } = ["Sunset (Fire)", "Ice (Blue)", "Rainbow", "Forest (Green)"];

    [ObservableProperty]
    private string _selectedPalette = "Sunset (Fire)";

    public List<string> FractalTypes { get; } = ["Mandelbrot", "Julia", "Burning Ship", "Tricorn", "Celtic", "Buffalo", "Multibrot 3"];

    [ObservableProperty]
    private string _selectedFractalType = "Mandelbrot";

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

    // Viewport dimensions, bound to the Canvas/Image size
    [ObservableProperty]
    private int _viewportWidth = 800;

    [ObservableProperty]
    private int _viewportHeight = 600;

    public Rect SelectionRectangle => new Rect(
        Math.Min(SelectionStart.X, SelectionEnd.X),
        Math.Min(SelectionStart.Y, SelectionEnd.Y),
        Math.Abs(SelectionEnd.X - SelectionStart.X),
        Math.Abs(SelectionEnd.Y - SelectionStart.Y)
    );

    public MainViewModel(IFractalGenerator fractalGenerator, IZoomService zoomService)
    {
        _gpuGenerator = fractalGenerator;
        _zoomService = zoomService;
        _zoomService.Reset(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        _ = GenerateFractalAsync();
    }

    // Default constructor for designer
    public MainViewModel()
    {
        _gpuGenerator = new ParallelFractalGenerator();
        _zoomService = new ZoomService();
        _zoomService.Reset(ViewportWidth, ViewportHeight);
    }

    partial void OnSelectionStartChanged(Point value)
    {
        OnPropertyChanged(nameof(SelectionRectangle));
    }

    partial void OnSelectionEndChanged(Point value)
    {
        OnPropertyChanged(nameof(SelectionRectangle));
    }

    partial void OnSelectedPaletteChanged(string value)
    {
        _ = GenerateFractalAsync();
    }

    partial void OnSelectedFractalTypeChanged(string value)
    {
        IsJuliaSettingsVisible = value == "Julia";
        _ = GenerateFractalAsync();
    }

    partial void OnJuliaRealChanged(string value)
    {
        _ = GenerateFractalAsync();
    }

    partial void OnJuliaImagChanged(string value)
    {
        _ = GenerateFractalAsync();
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

            int paletteId = SelectedPalette switch
            {
                "Ice (Blue)" => 2,
                "Rainbow" => 3,
                "Forest (Green)" => 4,
                _ => 1 // Sunset (Fire)
            };

            FractalType type = SelectedFractalType switch
            {
                "Julia" => FractalType.Julia,
                "Burning Ship" => FractalType.BurningShip,
                "Tricorn" => FractalType.Tricorn,
                "Celtic" => FractalType.Celtic,
                "Buffalo" => FractalType.Buffalo,
                "Multibrot 3" => FractalType.Multibrot3,
                _ => FractalType.Mandelbrot
            };

            var settings = new FractalSettings(
                type,
                GetJuliaCReal(),
                GetJuliaCImag()
            );

            byte[] pixelData = await activeGenerator.GenerateAsync(viewport, iterations, paletteId, settings, token);
            stopwatch.Stop();

            if (!token.IsCancellationRequested)
            {
                var bitmap = new WriteableBitmap(
                    new PixelSize(viewport.ImageWidth, viewport.ImageHeight),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Opaque);

                using (var frameBuffer = bitmap.Lock())
                {
                    Marshal.Copy(pixelData, 0, frameBuffer.Address, pixelData.Length);
                }

                FractalImage = bitmap;

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
        _ = GenerateFractalAsync();
    }

    [RelayCommand]
    private void Reset()
    {
        _zoomService.Reset(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        _ = GenerateFractalAsync();
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
        _ = GenerateFractalAsync();
    }

    public void OnSizeChanged(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == ViewportWidth && height == ViewportHeight)) return;

        ViewportWidth = width;
        ViewportHeight = height;

        _zoomService.ResizeCurrent(width, height);
        _ = GenerateFractalAsync();
    }

    private void UpdateCanZoomOut()
    {
        CanZoomOut = _zoomService.CanZoomOut;
    }

    [RelayCommand]
    private void SaveImage()
    {
        if (FractalImage == null) return;

        try
        {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SavedImages");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string fileName = $"{SelectedFractalType.Replace(" ", "")}_Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(folderPath, fileName);

            FractalImage.Save(filePath);
            StatusText = $"Saved to {fileName} under base/SavedImages/";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
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

        var viewport = _zoomService.CurrentViewport;
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
        _ = GenerateFractalAsync();
    }

    public void EndPan()
    {
        IsPanning = false;
    }
}

