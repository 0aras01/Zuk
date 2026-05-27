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
            // Max iterations can scale with zoom level later
            int maxIterations = 200;

            byte[] pixelData = await _fractalGenerator.GenerateAsync(viewport, maxIterations, token);

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
                stopwatch.Stop();
                StatusText = $"Generated in {stopwatch.ElapsedMilliseconds} ms";
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
        _zoomService.ZoomOut();
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

        // Optionally, maintain current zoom area but adapt aspect ratio, or just reset to new dimensions
        // For simplicity, we just trigger a redraw with current plane but new dimensions
        var currentPlane = _zoomService.CurrentViewport.Plane;
        _zoomService.ZoomTo(currentPlane, width, height); // This adds to history, maybe we shouldn't on resize, but it works.
        _ = GenerateFractalAsync();
    }

    private void UpdateCanZoomOut()
    {
        CanZoomOut = _zoomService.CanZoomOut;
    }
}
