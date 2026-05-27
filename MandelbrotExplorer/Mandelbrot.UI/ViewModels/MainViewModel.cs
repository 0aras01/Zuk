using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Mandelbrot.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFractalGenerator _fractalGenerator;
    private readonly IZoomService _zoomService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private SoftwareBitmapSource? _fractalImageSource;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isSelecting;

    [ObservableProperty]
    private double _selectionStartX;
    [ObservableProperty]
    private double _selectionStartY;

    [ObservableProperty]
    private double _selectionEndX;
    [ObservableProperty]
    private double _selectionEndY;

    [ObservableProperty]
    private bool _canZoomOut;

    // Viewport dimensions, bound to the Canvas/Image size
    [ObservableProperty]
    private int _viewportWidth = 800;

    [ObservableProperty]
    private int _viewportHeight = 600;

    public double SelectionRectangleX => Math.Min(SelectionStartX, SelectionEndX);
    public double SelectionRectangleY => Math.Min(SelectionStartY, SelectionEndY);
    public double SelectionRectangleWidth => Math.Abs(SelectionEndX - SelectionStartX);
    public double SelectionRectangleHeight => Math.Abs(SelectionEndY - SelectionStartY);

    public MainViewModel(IFractalGenerator fractalGenerator, IZoomService zoomService)
    {
        _fractalGenerator = fractalGenerator;
        _zoomService = zoomService;
        _zoomService.Reset(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        _ = GenerateFractalAsync();
    }

    private void UpdateSelectionRectangleProperties()
    {
        OnPropertyChanged(nameof(SelectionRectangleX));
        OnPropertyChanged(nameof(SelectionRectangleY));
        OnPropertyChanged(nameof(SelectionRectangleWidth));
        OnPropertyChanged(nameof(SelectionRectangleHeight));
    }

    partial void OnSelectionStartXChanged(double value) => UpdateSelectionRectangleProperties();
    partial void OnSelectionStartYChanged(double value) => UpdateSelectionRectangleProperties();
    partial void OnSelectionEndXChanged(double value) => UpdateSelectionRectangleProperties();
    partial void OnSelectionEndYChanged(double value) => UpdateSelectionRectangleProperties();

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
            int maxIterations = 200;

            byte[] pixelData = await _fractalGenerator.GenerateAsync(viewport, maxIterations, token);

            if (!token.IsCancellationRequested)
            {
                var softwareBitmap = new SoftwareBitmap(
                    BitmapPixelFormat.Bgra8,
                    viewport.ImageWidth,
                    viewport.ImageHeight,
                    BitmapAlphaMode.Premultiplied);

                softwareBitmap.CopyFromBuffer(pixelData.AsBuffer());

                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(softwareBitmap);

                FractalImageSource = source;
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

    public void OnPointerPressed(double x, double y)
    {
        SelectionStartX = x;
        SelectionStartY = y;
        SelectionEndX = x;
        SelectionEndY = y;
        IsSelecting = true;
    }

    public void OnPointerMoved(double x, double y)
    {
        if (IsSelecting)
        {
            SelectionEndX = x;
            SelectionEndY = y;
        }
    }

    public void OnPointerReleased(double x, double y)
    {
        if (!IsSelecting) return;

        IsSelecting = false;
        SelectionEndX = x;
        SelectionEndY = y;

        if (SelectionRectangleWidth < 10 || SelectionRectangleHeight < 10)
        {
            // Ignore tiny selections
            return;
        }

        // Map selection rectangle to Complex Plane
        var topLeft = CoordinateMapper.PixelToComplex((int)SelectionRectangleX, (int)SelectionRectangleY, _zoomService.CurrentViewport);
        var bottomRight = CoordinateMapper.PixelToComplex((int)(SelectionRectangleX + SelectionRectangleWidth), (int)(SelectionRectangleY + SelectionRectangleHeight), _zoomService.CurrentViewport);

        var newPlane = new ComplexPlane(
            Math.Min(topLeft.real, bottomRight.real),
            Math.Max(topLeft.real, bottomRight.real),
            Math.Min(topLeft.imag, bottomRight.imag),
            Math.Max(topLeft.imag, bottomRight.imag)
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

        var currentPlane = _zoomService.CurrentViewport.Plane;
        _zoomService.ZoomTo(currentPlane, width, height);
        _ = GenerateFractalAsync();
    }

    private void UpdateCanZoomOut()
    {
        CanZoomOut = _zoomService.CanZoomOut;
    }
}
