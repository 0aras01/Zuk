using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;

namespace Mandelbrot.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFractalGenerator _fractalGenerator;
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
        _fractalGenerator = fractalGenerator;
        _zoomService = zoomService;
        _zoomService.Reset(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        _ = GenerateFractalAsync();
    }

    // Default constructor for designer
    public MainViewModel()
    {
        _fractalGenerator = new ParallelFractalGenerator();
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

            byte[] pixelData = await _fractalGenerator.GenerateAsync(viewport, iterations, token);
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

                double zoomFactor = 3.5 / (viewport.Plane.RealMax - viewport.Plane.RealMin);
                StatusText = $"{stopwatch.ElapsedMilliseconds} ms | {iterations} iter | {zoomFactor:F1}× ({_fractalGenerator.Name})";
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
            Math.Min(topLeft.real, bottomRight.real),
            Math.Max(topLeft.real, bottomRight.real),
            Math.Min(topLeft.imag, bottomRight.imag), // lower imag value
            Math.Max(topLeft.imag, bottomRight.imag)  // higher imag value
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
}
