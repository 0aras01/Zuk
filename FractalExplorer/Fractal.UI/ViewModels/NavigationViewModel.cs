using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public partial class NavigationViewModel : ObservableObject
{
    private readonly IZoomService _zoomService;
    private readonly BookmarkService _bookmarkService;
    private readonly ILogger<NavigationViewModel>? _logger;

    public IZoomService ZoomService => _zoomService;

    private Timer? _panDebounceTimer;
    private const int PanDebounceMs = 50;

    public MainViewModel Main { get; internal set; } = null!;

    [ObservableProperty]
    private bool _isPanning;

    [ObservableProperty]
    private double _panOffsetX;

    [ObservableProperty]
    private double _panOffsetY;

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

    [ObservableProperty]
    private int _viewportWidth = 800;

    [ObservableProperty]
    private int _viewportHeight = 600;

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

    public NavigationViewModel()
    {
        _zoomService = new ZoomService();
        _bookmarkService = new BookmarkService();
        Bookmarks = new ObservableCollection<BookmarkEntry>();
    }

    public NavigationViewModel(IZoomService zoomService, BookmarkService bookmarkService, ILogger<NavigationViewModel> logger)
    {
        _zoomService = zoomService;
        _bookmarkService = bookmarkService;
        _logger = logger;
        _zoomService.Reset(ViewportWidth, ViewportHeight);
        Bookmarks = new ObservableCollection<BookmarkEntry>(_bookmarkService.LoadBookmarks());
        UpdateCanZoomOut();
    }

    partial void OnSelectionStartChanged(Point value) => OnPropertyChanged(nameof(SelectionRectangle));
    partial void OnSelectionEndChanged(Point value) => OnPropertyChanged(nameof(SelectionRectangle));

    partial void OnSelectedBookmarkChanged(BookmarkEntry? value)
    {
        if (value == null) return;

        _logger?.LogInformation("Selected bookmark: {BookmarkName}", value.Name);

        Main.Rendering.IsBatchUpdating = true;
        try
        {
            // Update Julia parameters BEFORE changing the fractal type to prevent rendering with stale parameters
            if (value.FractalType == FractalType.Julia)
            {
                Main.Rendering.JuliaReal = value.JuliaCReal.ToString(CultureInfo.InvariantCulture);
                Main.Rendering.JuliaImag = value.JuliaCImag.ToString(CultureInfo.InvariantCulture);
            }

            Main.Rendering.SelectedFractalType = value.FractalType;
            if (Main.Rendering.Palettes.Count > 0)
            {
                var palette = System.Linq.Enumerable.FirstOrDefault(Main.Rendering.Palettes, p => p.Name == value.PaletteName) 
                              ?? Main.Rendering.Palettes[0];
                Main.Rendering.SelectedPalette = palette;
            }
            Main.Rendering.AdaptiveIterations = value.Iterations;
        }
        finally
        {
            Main.Rendering.IsBatchUpdating = false;
        }

        _zoomService.ZoomTo(value.Plane, ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        Main.Rendering.RequestRender();
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
            FractalType = Main.Rendering.SelectedFractalType,
            Plane = viewport.Plane,
            PaletteName = Main.Rendering.SelectedPalette?.Name ?? "Sunset",
            Iterations = Main.Rendering.AdaptiveIterations,
            JuliaCReal = (double)Main.Rendering.GetJuliaCReal(),
            JuliaCImag = (double)Main.Rendering.GetJuliaCImag()
        };
        Bookmarks.Add(entry);
        _bookmarkService.SaveBookmarks(new List<BookmarkEntry>(Bookmarks));
        _logger?.LogInformation("Added bookmark: {BookmarkName}", entry.Name);
        NewBookmarkName = "";
    }

    private bool CanAddBookmark() => !string.IsNullOrWhiteSpace(NewBookmarkName);

    [RelayCommand]
    private void DeleteBookmark(BookmarkEntry? bookmark)
    {
        if (bookmark == null) return;
        Bookmarks.Remove(bookmark);
        _bookmarkService.SaveBookmarks(new List<BookmarkEntry>(Bookmarks));
        _logger?.LogInformation("Deleted bookmark: {BookmarkName}", bookmark.Name);
        if (SelectedBookmark == bookmark)
        {
            SelectedBookmark = null;
        }
    }

    [RelayCommand]
    public void ZoomOut()
    {
        _zoomService.ZoomOut(ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        Main.Rendering.RequestRender();
    }

    [RelayCommand]
    public void Reset()
    {
        _zoomService.Reset(ViewportWidth, ViewportHeight);
        
        IsPanning = false;
        IsSelecting = false;
        _panDebounceTimer?.Dispose();
        _panDebounceTimer = null;

        Main.Rendering.ResetSettings();

        UpdateCanZoomOut();
        Main.Rendering.RequestRender();
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
            return;
        }

        var topLeft = CoordinateMapper.PixelToComplex((int)rect.TopLeft.X, (int)rect.TopLeft.Y, _zoomService.CurrentViewport);
        var bottomRight = CoordinateMapper.PixelToComplex((int)rect.BottomRight.X, (int)rect.BottomRight.Y, _zoomService.CurrentViewport);

        var newPlane = new ComplexPlane(
            DoubleDouble.Min(topLeft.real, bottomRight.real),
            DoubleDouble.Max(topLeft.real, bottomRight.real),
            DoubleDouble.Min(topLeft.imag, bottomRight.imag),
            DoubleDouble.Max(topLeft.imag, bottomRight.imag)
        );

        _zoomService.ZoomTo(newPlane, ViewportWidth, ViewportHeight);
        UpdateCanZoomOut();
        Main.Rendering.RequestRender();
    }

    public void OnSizeChanged(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == ViewportWidth && height == ViewportHeight)) return;

        ViewportWidth = width;
        ViewportHeight = height;

        _zoomService.ResizeCurrent(width, height);
        Main.Rendering.RequestRender();
    }

    public void UpdateCanZoomOut()
    {
        CanZoomOut = _zoomService.CanZoomOut;
    }

    public void StartPan(Point position)
    {
        _panStartPoint = position;
        _panStartPlane = _zoomService.CurrentViewport.Plane;
        PanOffsetX = 0;
        PanOffsetY = 0;
        IsPanning = true;
    }

    public void MovePan(Point position)
    {
        if (!IsPanning) return;

        double deltaX = position.X - _panStartPoint.X;
        double deltaY = position.Y - _panStartPoint.Y;

        PanOffsetX = deltaX;
        PanOffsetY = deltaY;

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
        Main?.UpdateMinimapViewportRect();
    }

    public void EndPan()
    {
        IsPanning = false;
        _panDebounceTimer?.Dispose();
        _panDebounceTimer = null;
        Main.Rendering.RequestRender();
    }

    public void ResetPanOffset()
    {
        PanOffsetX = 0;
        PanOffsetY = 0;
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
        Main.Rendering.RequestRender();
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
        Main.Rendering.RequestRender();
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
        Main.Rendering.RequestRender();
    }

    public DoubleDouble LastCursorRe { get; private set; } = 0.0;
    public DoubleDouble LastCursorIm { get; private set; } = 0.0;

    public void UpdateCursorCoordinates(Point position)
    {
        var viewport = _zoomService.CurrentViewport;
        if (viewport.ImageWidth <= 0 || viewport.ImageHeight <= 0) return;

        var (re, im) = CoordinateMapper.PixelToComplex((int)position.X, (int)position.Y, viewport);
        LastCursorRe = re;
        LastCursorIm = im;
        RefreshCursorCoordinatesText();
    }

    public void RefreshCursorCoordinatesText()
    {
        string sign = (double)LastCursorIm >= 0 ? "+" : "-";
        DoubleDouble absIm = LastCursorIm.Abs();
        if (Main.IsScientificNotationEnabled)
        {
            CursorCoordinatesText = $"z = {((double)LastCursorRe):E6} {sign} {((double)absIm):E6}i";
        }
        else
        {
            CursorCoordinatesText = $"z = {((double)LastCursorRe):G6} {sign} {((double)absIm):G6}i";
        }
    }

    public void CancelSelection()
    {
        IsSelecting = false;
        SelectionStart = default;
        SelectionEnd = default;
    }
}
