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

    [RelayCommand]
    private void OpenPaletteEditor()
    {
        // TODO: Implement palette editor
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

                Main.Diagnostics.CenterCoordinatesText = $"Re: {centerReal.ToFullString()}\nIm: {centerImag.ToFullString()}";
                Main.Diagnostics.SpanText = $"{spanReal.ToFullString()} × {spanImag.ToFullString()}";
                Main.Diagnostics.ResolutionText = $"{viewport.ImageWidth} × {viewport.ImageHeight}";
                Main.Diagnostics.RenderTimeText = $"{stopwatch.ElapsedMilliseconds} ms";
                Main.Diagnostics.IterationsText = $"{iterations}";
                Main.Diagnostics.EngineText = $"{activeGenerator.Name}";
                Main.Diagnostics.ZoomText = $"{zoomFactor:N1}×";

                StatusText = $"{stopwatch.ElapsedMilliseconds} ms | {iterations} iter | {zoomFactor:F1}× ({activeGenerator.Name})";
                _logger?.LogInformation("Render completed in {ElapsedMs} ms using {EngineName}", stopwatch.ElapsedMilliseconds, activeGenerator.Name);
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
}
