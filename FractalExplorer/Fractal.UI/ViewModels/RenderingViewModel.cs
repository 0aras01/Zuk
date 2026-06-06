using System;
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
using Fractal.UI.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Fractal.UI.ViewModels;

public partial class RenderingViewModel : ObservableObject
{
    private readonly IFractalGenerator _gpuGenerator;
    private readonly IFractalGenerator _cpuGenerator = new ParallelFractalGenerator();
    private readonly ILogger<RenderingViewModel>? _logger;

    public IFractalGenerator GpuGenerator => _gpuGenerator;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _overlayCts;

    // Adaptive iteration budget
    private const double TargetRenderMs = 100.0;
    private const int MinIterations = 200;
    private const int MaxIterations = 50_000;
    
    public int AdaptiveIterations { get; set; } = 500;

    // Buffer reuse
    private byte[]? _pixelBuffer;
    private WriteableBitmap? _reusableBitmap;
    private int _lastWidth, _lastHeight;

    public MainViewModel Main { get; internal set; } = null!;

    [ObservableProperty]
    private WriteableBitmap? _fractalImage;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isAnimating;

    [ObservableProperty]
    private bool _isCancelOverlayVisible;

    public ObservableCollection<GradientPalette> Palettes { get; } = new ObservableCollection<GradientPalette>();

    [ObservableProperty]
    private GradientPalette _selectedPalette = new GradientPalette();

    [ObservableProperty]
    private bool _isColorCycling;

    [ObservableProperty]
    private double _paletteOffset;

    private readonly object _stateLock = new object();
    private byte[]? _colorCyclingPixelBuffer;
    private double[]? _lastIterations;

    public FractalType[] FractalTypes { get; } = Enum.GetValues<FractalType>();

    [ObservableProperty]
    private FractalType _selectedFractalType = FractalType.Mandelbrot;

    [ObservableProperty]
    private string _juliaReal = "-0.7";

    [ObservableProperty]
    private string _juliaImag = "0.27015";

    [ObservableProperty]
    private bool _isJuliaSettingsVisible;

    public RenderingViewModel()
    {
        _gpuGenerator = new ParallelFractalGenerator();
        LoadPalettes();
    }

    public RenderingViewModel(IFractalGenerator fractalGenerator, IZoomService zoomService, ILogger<RenderingViewModel> logger)
    {
        _gpuGenerator = fractalGenerator;
        _logger = logger;
        LoadPalettes();
    }

    private void LoadPalettes()
    {
        var paletteService = new PaletteService();
        foreach (var p in paletteService.LoadPalettes())
        {
            Palettes.Add(p);
        }
        if (Palettes.Count > 0)
        {
            SelectedPalette = Palettes[0];
        }
    }

    public bool IsBatchUpdating { get; set; }

    partial void OnSelectedPaletteChanged(GradientPalette value)
    {
        if (!IsBatchUpdating) RequestRender();
    }

    public Action<GradientPalette?>? OpenPaletteEditorAction { get; set; }

    [RelayCommand]
    private void OpenPaletteEditor()
    {
        if (Main != null)
        {
            Main.IsColorPaletteEditorVisible = true;
        }
        OpenPaletteEditorAction?.Invoke(SelectedPalette);
    }

    partial void OnSelectedFractalTypeChanged(FractalType value)
    {
        IsJuliaSettingsVisible = value == FractalType.Julia;
        if (!IsBatchUpdating) RequestRender();
    }

    partial void OnJuliaRealChanged(string value)
    {
        if (!IsBatchUpdating) RequestRender();
    }

    partial void OnJuliaImagChanged(string value)
    {
        if (!IsBatchUpdating) RequestRender();
    }

    public void ResetSettings()
    {
        IsBatchUpdating = true;
        try
        {
            SelectedFractalType = FractalType.Mandelbrot;
            if (Palettes.Count > 0) SelectedPalette = Palettes[0];
            JuliaReal = "-0.7";
            JuliaImag = "0.27015";
            AdaptiveIterations = 500;
            IsAnimating = false;
            IsCancelOverlayVisible = false;
            StatusText = "Ready";
        }
        finally
        {
            IsBatchUpdating = false;
        }
    }

    public DoubleDouble GetJuliaCReal()
    {
        if (double.TryParse(JuliaReal, System.Globalization.CultureInfo.InvariantCulture, out double val))
            return val;
        return -0.7;
    }

    public DoubleDouble GetJuliaCImag()
    {
        if (double.TryParse(JuliaImag, System.Globalization.CultureInfo.InvariantCulture, out double val))
            return val;
        return 0.27015;
    }

    [RelayCommand]
    public async Task GenerateFractalAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _overlayCts?.Cancel();
        _overlayCts = new CancellationTokenSource();
        var overlayToken = _overlayCts.Token;

        IsCancelOverlayVisible = false;

        // Start a 5 seconds task to show the Cancel button overlay if rendering exceeds 5 seconds
        _ = Task.Delay(5000, overlayToken).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    IsCancelOverlayVisible = true;
                }
            }
        }, TaskContinuationOptions.ExecuteSynchronously);

        StatusText = "Generating...";
        _logger?.LogInformation("Render request initiated for {FractalType} (Palette: {Palette})", SelectedFractalType, SelectedPalette);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var viewport = Main.Navigation.ZoomService.CurrentViewport;
            int iterations = AdaptiveIterations;

            double zoomFactor = 3.5 / (double)(viewport.Plane.RealMax - viewport.Plane.RealMin);

            var activeGenerator = (zoomFactor > 1e10 && _gpuGenerator.IsGpuAccelerated)
                ? _cpuGenerator
                : _gpuGenerator;



            var settings = new FractalSettings(
                SelectedFractalType,
                GetJuliaCReal(),
                GetJuliaCImag()
            );

            int requiredBytes = viewport.ImageWidth * viewport.ImageHeight * 4;
            if (_pixelBuffer == null || _pixelBuffer.Length != requiredBytes)
                _pixelBuffer = new byte[requiredBytes];

            var paletteToUse = SelectedPalette ?? (Palettes.Count > 0 ? Palettes[0] : new GradientPalette());
            var (pixelData, iterationsData) = await activeGenerator.GenerateAsync(viewport, iterations, paletteToUse, PaletteOffset, settings, token);
            stopwatch.Stop();

            if (!token.IsCancellationRequested)
            {
                _overlayCts?.Cancel();
                _overlayCts = null;
                IsCancelOverlayVisible = false;

                lock (_stateLock)
                {
                    _lastIterations = iterationsData;

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

                    if (_lastWidth == viewport.ImageWidth && _lastHeight == viewport.ImageHeight)
                    {
                        using (var frameBuffer = _reusableBitmap.Lock())
                        {
                            Marshal.Copy(pixelData, 0, frameBuffer.Address, pixelData.Length);
                        }
                    }
                }

                FractalImage = null;
                FractalImage = _reusableBitmap;
                
                if (!Main.Navigation.IsPanning)
                {
                    Main.Navigation.ResetPanOffset();
                }

                double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                if (elapsedMs > 0)
                {
                    double ratio = TargetRenderMs / elapsedMs;
                    int proposed = (int)(iterations * ratio);
                    AdaptiveIterations = Math.Clamp(
                        (iterations + proposed) / 2,
                        MinIterations,
                        MaxIterations);
                }

                var centerReal = (viewport.Plane.RealMin + viewport.Plane.RealMax) * 0.5;
                var centerImag = (viewport.Plane.ImagMin + viewport.Plane.ImagMax) * 0.5;
                var spanReal = viewport.Plane.RealMax - viewport.Plane.RealMin;
                var spanImag = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

                if (Main.IsScientificNotationEnabled)
                {
                    Main.Diagnostics.CenterCoordinatesText = $"Re: {((double)centerReal):E10}\nIm: {((double)centerImag):E10}";
                    Main.Diagnostics.SpanText = $"{((double)spanReal):E10} × {((double)spanImag):E10}";
                }
                else
                {
                    Main.Diagnostics.CenterCoordinatesText = $"Re: {centerReal.ToFullString()}\nIm: {centerImag.ToFullString()}";
                    Main.Diagnostics.SpanText = $"{spanReal.ToFullString()} × {spanImag.ToFullString()}";
                }
                Main.Diagnostics.ResolutionText = $"{viewport.ImageWidth} × {viewport.ImageHeight}";
                Main.Diagnostics.RenderTimeText = $"{stopwatch.ElapsedMilliseconds} ms";
                Main.Diagnostics.IterationsText = $"{iterations}";
                Main.Diagnostics.EngineText = $"{activeGenerator.Name}";
                Main.Diagnostics.ZoomText = $"{zoomFactor:N1}×";

                StatusText = $"{stopwatch.ElapsedMilliseconds} ms | {iterations} iter | {zoomFactor:F1}× ({activeGenerator.Name})";
                _logger?.LogInformation("Render completed in {ElapsedMs} ms using {EngineName}", stopwatch.ElapsedMilliseconds, activeGenerator.Name);

                Main?.UpdateMinimapViewportRect();
                _ = Main?.GenerateMinimapAsync();
            }
        }
        catch (OperationCanceledException)
        {
            if (_cts?.Token != token) return;
            
            _overlayCts?.Cancel();
            _overlayCts = null;
            IsCancelOverlayVisible = false;
            StatusText = LocalizationService.Instance["StatusCancelled"];
            _logger?.LogInformation("Render request cancelled.");
        }
        catch (Exception ex)
        {
            if (_cts?.Token != token) return;
            
            _overlayCts?.Cancel();
            _overlayCts = null;
            IsCancelOverlayVisible = false;
            StatusText = $"Error: {ex.Message}";
            _logger?.LogError(ex, "Render request failed.");
        }
    }

    [RelayCommand]
    public void CancelRender()
    {
        _cts?.Cancel();
        _overlayCts?.Cancel();
        IsCancelOverlayVisible = false;
        StatusText = LocalizationService.Instance["StatusCancelled"];
        _logger?.LogInformation("Render cancelled by user.");
    }

    public void ApplyColorCyclingFrame(int width, int height)
    {
        if (!IsColorCycling || _lastIterations == null || SelectedPalette == null) return;

        lock (_stateLock)
        {
            if (_reusableBitmap == null || _lastWidth != width || _lastHeight != height) return;

            int count = width * height;
            if (_lastIterations == null || _lastIterations.Length < count) return;

            if (_colorCyclingPixelBuffer == null || _colorCyclingPixelBuffer.Length < count * 4)
                _colorCyclingPixelBuffer = new byte[count * 4];

            for (int i = 0; i < count; i++)
            {
                SelectedPalette.GetColor(_lastIterations[i], PaletteOffset, out byte r, out byte g, out byte b);
                int idx = i * 4;
                _colorCyclingPixelBuffer[idx] = b;
                _colorCyclingPixelBuffer[idx + 1] = g;
                _colorCyclingPixelBuffer[idx + 2] = r;
                _colorCyclingPixelBuffer[idx + 3] = 255;
            }

            using (var frameBuffer = _reusableBitmap.Lock())
            {
                Marshal.Copy(_colorCyclingPixelBuffer, 0, frameBuffer.Address, count * 4);
            }
        }
    }

    public void RequestRender()
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
            var viewport = Main.Navigation.ZoomService.CurrentViewport;
            var plane = viewport.Plane;
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

            Main.Navigation.ZoomService.ZoomTo(newPlane, Main.Navigation.ViewportWidth, Main.Navigation.ViewportHeight);
            Main.Navigation.UpdateCanZoomOut();

            await GenerateFractalAsync();
        }
    }

    [RelayCommand]
    public async Task SaveImageAsync()
    {
        if (FractalImage == null) return;

        try
        {
            string? filePath = null;
            if (Main.SaveFileDialogAction != null)
            {
                filePath = await Main.SaveFileDialogAction();
                if (filePath == null) return; // User cancelled
            }
            else
            {
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SavedImages");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string fileName = $"{SelectedFractalType.ToString().Replace(" ", "")}_Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                filePath = Path.Combine(folderPath, fileName);
            }

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            FractalImage.Save(filePath);
            StatusText = $"Saved to {Path.GetFileName(filePath)}";
            _logger?.LogInformation("Saved image to {Path}", filePath);
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
            _logger?.LogError(ex, "Failed to save image");
        }
    }

    [RelayCommand]
    public async Task CopyToClipboardAsync()
    {
        if (FractalImage == null || Main.CopyToClipboardAction == null) return;

        try
        {
            await Main.CopyToClipboardAction();
            StatusText = "Image copied to clipboard";
            _logger?.LogInformation("Copied image to clipboard");
        }
        catch (Exception ex)
        {
            StatusText = $"Clipboard error: {ex.Message}";
            _logger?.LogError(ex, "Failed to copy image to clipboard");
        }
    }

    public async Task ExportHighResBmpAsync(string filePath, int largeWidth, int largeHeight, CancellationToken ct)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 1. Create file and write BMP headers
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            // Write BMP Header (14 bytes)
            byte[] fileHeader = new byte[14];
            fileHeader[0] = (byte)'B';
            fileHeader[1] = (byte)'M';
            
            int fileSize = 54 + largeWidth * largeHeight * 4;
            BitConverter.TryWriteBytes(fileHeader.AsSpan(2), fileSize);
            BitConverter.TryWriteBytes(fileHeader.AsSpan(10), 54); // Offset to pixel data
            
            await fs.WriteAsync(fileHeader, ct);
            
            // Write DIB Header (BITMAPINFOHEADER, 40 bytes)
            byte[] dibHeader = new byte[40];
            BitConverter.TryWriteBytes(dibHeader.AsSpan(0), 40); // Header size
            BitConverter.TryWriteBytes(dibHeader.AsSpan(4), largeWidth);
            BitConverter.TryWriteBytes(dibHeader.AsSpan(8), largeHeight);
            BitConverter.TryWriteBytes(dibHeader.AsSpan(12), (short)1); // Planes
            BitConverter.TryWriteBytes(dibHeader.AsSpan(14), (short)32); // Bits per pixel (32-bit BGRA)
            BitConverter.TryWriteBytes(dibHeader.AsSpan(16), 0); // BI_RGB (uncompressed)
            BitConverter.TryWriteBytes(dibHeader.AsSpan(20), largeWidth * largeHeight * 4); // Image size
            
            await fs.WriteAsync(dibHeader, ct);
            
            // Pre-allocate the file size to avoid fragmenting
            fs.SetLength(fileSize);
        }

        // 2. Determine tiling layout
        int tileW = 1920;
        int tileH = 1080;
        
        int cols = (largeWidth + tileW - 1) / tileW;
        int rows = (largeHeight + tileH - 1) / tileH;
        int totalTiles = cols * rows;
        
        var largeViewport = Main.Navigation.ZoomService.CurrentViewport;
        var largePlane = largeViewport.Plane;
        
        DoubleDouble realRange = largePlane.RealMax - largePlane.RealMin;
        DoubleDouble imagRange = largePlane.ImagMax - largePlane.ImagMin;
        
        // Settings for generator
        int maxIterations = AdaptiveIterations;
        var palette = SelectedPalette;
        double paletteOffset = PaletteOffset;
        var settings = new FractalSettings
        {
            Type = SelectedFractalType,
            JuliaCReal = GetJuliaCReal(),
            JuliaCImag = GetJuliaCImag()
        };

        // 3. Render and write tiles
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: true))
        {
            int tileIndex = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (ct.IsCancellationRequested) return;
                    
                    int xOffset = c * tileW;
                    int yOffset = r * tileH;
                    int currentTileW = Math.Min(tileW, largeWidth - xOffset);
                    int currentTileH = Math.Min(tileH, largeHeight - yOffset);
                    
                    // Map tile coordinates
                    DoubleDouble tileRealMin = largePlane.RealMin + (realRange * xOffset / largeWidth);
                    DoubleDouble tileRealMax = largePlane.RealMin + (realRange * (xOffset + currentTileW) / largeWidth);
                    DoubleDouble tileImagMax = largePlane.ImagMax - (imagRange * yOffset / largeHeight);
                    DoubleDouble tileImagMin = largePlane.ImagMax - (imagRange * (yOffset + currentTileH) / largeHeight);
                    
                    var tilePlane = new ComplexPlane(tileRealMin, tileRealMax, tileImagMin, tileImagMax);
                    var tileViewport = new Viewport(tilePlane, currentTileW, currentTileH);
                    
                    // Generate tile
                    var (tilePixels, _) = await GpuGenerator.GenerateAsync(tileViewport, maxIterations, palette, paletteOffset, settings, ct);
                    
                    // Write tile row-by-row
                    for (int y = 0; y < currentTileH; y++)
                    {
                        int largeY = yOffset + y;
                        int bmpRow = (largeHeight - 1) - largeY;
                        long seekPos = 54 + (long)bmpRow * largeWidth * 4 + (long)xOffset * 4;
                        
                        fs.Seek(seekPos, SeekOrigin.Begin);
                        
                        int tileRowStart = y * currentTileW * 4;
                        await fs.WriteAsync(tilePixels.AsMemory(tileRowStart, currentTileW * 4), ct);
                    }
                    
                    tileIndex++;
                    StatusText = $"Exporting high-resolution image... {tileIndex * 100 / totalTiles}%";
                }
            }
        }
    }
}
