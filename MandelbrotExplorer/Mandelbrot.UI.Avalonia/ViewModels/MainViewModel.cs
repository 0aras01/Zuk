using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;
using Mandelbrot.Core.Enums;
using Mandelbrot.Core.Export;
using System.Linq;

namespace Mandelbrot.UI.Avalonia.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFractalGenerator _fractalGenerator;
    private readonly IZoomService _zoomService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IFileExportService _fileExportService;
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

    public Rect SelectionRectangle => new Rect(
        Math.Min(SelectionStart.X, SelectionEnd.X),
        Math.Min(SelectionStart.Y, SelectionEnd.Y),
        Math.Abs(SelectionEnd.X - SelectionStart.X),
        Math.Abs(SelectionEnd.Y - SelectionStart.Y)
    );

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

    partial void OnSelectionStartChanged(Point value) => OnPropertyChanged(nameof(SelectionRectangle));
    partial void OnSelectionEndChanged(Point value) => OnPropertyChanged(nameof(SelectionRectangle));

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

    // A stub for export, it requires file dialog path from UI which will be implemented in code-behind
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

    public void OnPointerPressed(Point position)
    {
        SelectionStart = position;
        SelectionEnd = position;
        IsSelecting = true;
    }

    public void OnPointerMoved(Point position)
    {
        if (IsSelecting) SelectionEnd = position;
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
