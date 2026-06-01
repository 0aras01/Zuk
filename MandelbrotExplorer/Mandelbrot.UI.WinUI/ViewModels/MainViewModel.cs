using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;
using Mandelbrot.Core.Enums;
using Mandelbrot.Core.Export;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Mandelbrot.UI.WinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFractalGenerator _fractalGenerator;
    private readonly IZoomService _zoomService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IFileExportService _fileExportService;
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

    [ObservableProperty]
    private int _viewportWidth = 800;

    [ObservableProperty]
    private int _viewportHeight = 600;

    [ObservableProperty]
    private int _maxIterations = 200;

    [ObservableProperty]
    private ColorTheme _selectedTheme = ColorTheme.Classic;

    public ObservableCollection<ColorTheme> AvailableThemes { get; } = new ObservableCollection<ColorTheme>(Enum.GetValues<ColorTheme>());

    public ObservableCollection<Bookmark> Bookmarks { get; } = new ObservableCollection<Bookmark>();

    [ObservableProperty]
    private Bookmark? _selectedBookmark;

    public double SelectionRectangleX => Math.Min(SelectionStartX, SelectionEndX);
    public double SelectionRectangleY => Math.Min(SelectionStartY, SelectionEndY);
    public double SelectionRectangleWidth => Math.Abs(SelectionEndX - SelectionStartX);
    public double SelectionRectangleHeight => Math.Abs(SelectionEndY - SelectionStartY);

    public MainViewModel(
        IFractalGenerator fractalGenerator,
        IZoomService zoomService,
        IBookmarkService bookmarkService,
        IFileExportService fileExportService)
    {
        _fractalGenerator = fractalGenerator;
        _zoomService = zoomService;
        _bookmarkService = bookmarkService;
        _fileExportService = fileExportService;

        _zoomService.Reset(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        UpdateBookmarks();
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

    partial void OnSelectedThemeChanged(ColorTheme value) => _ = GenerateFractalAsync();
    partial void OnMaxIterationsChanged(int value) => _ = GenerateFractalAsync();

    partial void OnSelectedBookmarkChanged(Bookmark? value)
    {
        if (value != null)
        {
            _zoomService.ZoomTo(value.Plane, ViewportWidth, ViewportHeight);
            MaxIterations = value.MaxIterations;
            SelectedTheme = value.Theme;
            UpdateCanZoomOut();
            _ = GenerateFractalAsync();
        }
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

            byte[] pixelData = await _fractalGenerator.GenerateAsync(viewport, MaxIterations, SelectedTheme, token);

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
        catch (OperationCanceledException) { }
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

    [RelayCommand]
    private void AddBookmark()
    {
        var plane = _zoomService.CurrentViewport.Plane;
        var newBookmark = new Bookmark($"Zoom {Math.Round(plane.RealMin, 2)} + i{Math.Round(plane.ImagMin, 2)}", plane, MaxIterations, SelectedTheme);
        _bookmarkService.AddBookmark(newBookmark);
        UpdateBookmarks();
    }

    private void UpdateBookmarks()
    {
        Bookmarks.Clear();
        foreach (var b in _bookmarkService.GetBookmarks())
        {
            Bookmarks.Add(b);
        }
    }

    public async Task ExportToFileAsync(string filePath)
    {
        try
        {
            StatusText = "Exporting 4K image...";
            var exportViewport = new Viewport(_zoomService.CurrentViewport.Plane, 3840, 2160);
            byte[] pixelData = await _fractalGenerator.GenerateAsync(exportViewport, MaxIterations, SelectedTheme, CancellationToken.None);
            await _fileExportService.ExportImageAsync(pixelData, 3840, 2160, filePath);
            StatusText = "Export successful!";
        }
        catch (Exception ex)
        {
            StatusText = $"Export error: {ex.Message}";
        }
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

        if (SelectionRectangleWidth < 10 || SelectionRectangleHeight < 10) return;

        var topLeft = CoordinateMapper.PixelToComplex((int)SelectionRectangleX, (int)SelectionRectangleY, _zoomService.CurrentViewport);
        var bottomRight = CoordinateMapper.PixelToComplex((int)(SelectionRectangleX + SelectionRectangleWidth), (int)(SelectionRectangleY + SelectionRectangleHeight), _zoomService.CurrentViewport);

        var newPlane = new ComplexPlane(
            Math.Min(topLeft.real, bottomRight.real), Math.Max(topLeft.real, bottomRight.real),
            Math.Min(topLeft.imag, bottomRight.imag), Math.Max(topLeft.imag, bottomRight.imag)
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

    private void UpdateCanZoomOut() => CanZoomOut = _zoomService.CanZoomOut;
}
