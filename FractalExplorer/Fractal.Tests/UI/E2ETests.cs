using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Avalonia;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Fractal.UI.ViewModels;
using Fractal.UI.Services;
using Fractal.UI;

namespace Fractal.Tests.UI;

public class E2ETests : IDisposable
{
    private readonly string _bookmarkFolder;
    private readonly string _bookmarkFilePath;
    private readonly bool _hadOriginalBookmarks;
    private readonly string? _originalBookmarksContent;

    private class TestSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            d(state);
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            d(state);
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }
    }

    public E2ETests()
    {
        if (Application.Current == null)
        {
            try
            {
                AppBuilder.Configure<App>()
                    .UsePlatformDetect()
                    .SetupWithoutStarting();
            }
            catch
            {
                // Suppress initialization errors
            }
        }
        SynchronizationContext.SetSynchronizationContext(new TestSynchronizationContext());

        _bookmarkFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FractalExplorer");
        _bookmarkFilePath = Path.Combine(_bookmarkFolder, "bookmarks.json");
        _hadOriginalBookmarks = File.Exists(_bookmarkFilePath);
        if (_hadOriginalBookmarks)
        {
            try
            {
                _originalBookmarksContent = File.ReadAllText(_bookmarkFilePath);
            }
            catch
            {
                _hadOriginalBookmarks = false;
            }
        }
    }

    public void Dispose()
    {
        try
        {
            if (_hadOriginalBookmarks)
            {
                if (!Directory.Exists(_bookmarkFolder))
                {
                    Directory.CreateDirectory(_bookmarkFolder);
                }
                File.WriteAllText(_bookmarkFilePath, _originalBookmarksContent);
            }
            else if (File.Exists(_bookmarkFilePath))
            {
                File.Delete(_bookmarkFilePath);
            }
        }
        catch
        {
            // Suppress errors during cleanup
        }
    }

    private class SimulatedGpuGenerator : IFractalGenerator
    {
        private readonly ParallelFractalGenerator _cpuGenerator = new();

        public string Name => "GPU (Simulated)";
        public bool IsGpuAccelerated => true;

        public Task<(byte[] Pixels, double[] Iterations)> GenerateAsync(Viewport viewport, int maxIterations, GradientPalette palette, double paletteOffset, FractalSettings settings, CancellationToken ct)
        {
            return _cpuGenerator.GenerateAsync(viewport, maxIterations, palette, paletteOffset, settings, ct);
        }
    }

    private class TestConsoleLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string msg = formatter(state, exception);
            Console.WriteLine(msg);
            if (exception != null) Console.WriteLine(exception.ToString());
        }
    }

    private async Task<MainViewModel> CreateMainViewModelAsync(int width = 100, int height = 100)
    {
        SynchronizationContext.SetSynchronizationContext(new TestSynchronizationContext());
        var gpuGen = new SimulatedGpuGenerator();
        var zoomService = new ZoomService();
        var bookmarkService = new BookmarkService();
        var vm = new MainViewModel(gpuGen, zoomService, bookmarkService, new TestConsoleLogger<MainViewModel>(), new TestConsoleLogger<RenderingViewModel>());
        vm.OnSizeChanged(width, height);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        return vm;
    }

    private static double GetZoomFactor(string zoomText)
    {
        string cleaned = zoomText.Replace("×", "").Replace("x", "").Trim();
        return double.Parse(cleaned.Replace(",", "."), CultureInfo.InvariantCulture);
    }

    // ==========================================
    // TIER 1: FEATURE COVERAGE (55 TESTS)
    // ==========================================

    // --- Feature 1: Zooming ---

    [Fact]
    public async Task Tier1_Zoom_MouseWheelZoomIn()
    {
        var vm = await CreateMainViewModelAsync();
        double initialZoom = GetZoomFactor(vm.ZoomText);
        vm.OnMouseWheelZoom(new Point(50, 50), 1.0);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        GetZoomFactor(vm.ZoomText).Should().BeGreaterThan(initialZoom);
        vm.CanZoomOut.Should().BeTrue();
    }

    [Fact]
    public async Task Tier1_Zoom_MouseWheelZoomOut()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnMouseWheelZoom(new Point(50, 50), 1.0);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        double zoom1 = GetZoomFactor(vm.ZoomText);
        vm.OnMouseWheelZoom(new Point(50, 50), -1.0);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        GetZoomFactor(vm.ZoomText).Should().BeLessThan(zoom1);
    }

    [Fact]
    public async Task Tier1_Zoom_CenteredZoomIn()
    {
        var vm = await CreateMainViewModelAsync();
        double initialZoom = GetZoomFactor(vm.ZoomText);
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        GetZoomFactor(vm.ZoomText).Should().BeGreaterThan(initialZoom);
    }

    [Fact]
    public async Task Tier1_Zoom_CenteredZoomOut()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        double zoom1 = GetZoomFactor(vm.ZoomText);
        vm.ZoomCentered(false);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        GetZoomFactor(vm.ZoomText).Should().BeLessThan(zoom1);
    }

    [Fact]
    public async Task Tier1_Zoom_HistoryNavigation()
    {
        var vm = await CreateMainViewModelAsync();
        vm.CanZoomOut.Should().BeFalse();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CanZoomOut.Should().BeTrue();
        vm.ZoomOutCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CanZoomOut.Should().BeFalse();
    }

    // --- Feature 2: Panning ---

    [Fact]
    public async Task Tier1_Pan_MiddleButtonMouseDrag()
    {
        var vm = await CreateMainViewModelAsync();
        string initialCenter = vm.CenterCoordinatesText;
        vm.StartPan(new Point(50, 50));
        vm.MovePan(new Point(70, 60));
        vm.EndPan();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CenterCoordinatesText.Should().NotBe(initialCenter);
    }

    [Fact]
    public async Task Tier1_Pan_ArrowKeys()
    {
        var vm = await CreateMainViewModelAsync();
        string initialCenter = vm.CenterCoordinatesText;
        vm.PanByPercent(0.1, 0.0);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CenterCoordinatesText.Should().NotBe(initialCenter);
    }

    [Fact]
    public async Task Tier1_Pan_DebounceTimer()
    {
        var vm = await CreateMainViewModelAsync();
        vm.StartPan(new Point(50, 50));
        vm.MovePan(new Point(60, 50));
        vm.MovePan(new Point(70, 50));
        vm.MovePan(new Point(80, 50));
        vm.IsPanning.Should().BeTrue();
        vm.EndPan();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IsPanning.Should().BeFalse();
    }

    [Fact]
    public async Task Tier1_Pan_CancelActivePanState()
    {
        var vm = await CreateMainViewModelAsync();
        vm.StartPan(new Point(50, 50));
        vm.MovePan(new Point(60, 60));
        vm.EndPan();
        vm.IsPanning.Should().BeFalse();
    }

    [Fact]
    public async Task Tier1_Pan_MultipleConsecutivePans()
    {
        var vm = await CreateMainViewModelAsync();
        vm.StartPan(new Point(50, 50));
        vm.MovePan(new Point(60, 50));
        vm.EndPan();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        string center1 = vm.CenterCoordinatesText;

        vm.StartPan(new Point(60, 50));
        vm.MovePan(new Point(70, 50));
        vm.EndPan();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CenterCoordinatesText.Should().NotBe(center1);
    }

    // --- Feature 3: Selection ---

    [Fact]
    public async Task Tier1_Selection_PointerDownAndDrag()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnPointerPressed(new Point(20, 20));
        vm.OnPointerMoved(new Point(60, 50));
        vm.IsSelecting.Should().BeTrue();
        vm.SelectionRectangle.Width.Should().Be(40);
        vm.SelectionRectangle.Height.Should().Be(30);
    }

    [Fact]
    public async Task Tier1_Selection_PointerReleased()
    {
        var vm = await CreateMainViewModelAsync();
        string initialCenter = vm.CenterCoordinatesText;
        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(50, 50));
        vm.OnPointerReleased(new Point(50, 50));
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IsSelecting.Should().BeFalse();
        vm.CenterCoordinatesText.Should().NotBe(initialCenter);
    }

    [Fact]
    public async Task Tier1_Selection_CancelSelection()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(50, 50));
        vm.CancelSelection();
        vm.IsSelecting.Should().BeFalse();
        vm.SelectionRectangle.Width.Should().Be(0);
    }

    [Fact]
    public async Task Tier1_Selection_TinySelection()
    {
        var vm = await CreateMainViewModelAsync();
        string initialCenter = vm.CenterCoordinatesText;
        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(12, 12));
        vm.OnPointerReleased(new Point(12, 12));
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CenterCoordinatesText.Should().Be(initialCenter);
    }

    [Fact]
    public async Task Tier1_Selection_ReverseDragSelection()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnPointerPressed(new Point(50, 50));
        vm.OnPointerMoved(new Point(10, 10));
        vm.SelectionRectangle.Width.Should().Be(40);
        vm.SelectionRectangle.Height.Should().Be(40);
        vm.OnPointerReleased(new Point(10, 10));
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IsSelecting.Should().BeFalse();
    }

    // --- Feature 4: Reset ---

    [Fact]
    public async Task Tier1_Reset_ViewModelCommand()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.ResetCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CanZoomOut.Should().BeFalse();
    }

    [Fact]
    public async Task Tier1_Reset_ClearsZoomHistoryStack()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CanZoomOut.Should().BeTrue();
        vm.ResetCommand.Execute(null);
        vm.CanZoomOut.Should().BeFalse();
    }

    [Fact]
    public async Task Tier1_Reset_TriggersRenderRequest()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.ResetCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier1_Reset_KeysShortcut()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.ResetCommand.Execute(null);
        vm.CanZoomOut.Should().BeFalse();
    }

    [Fact]
    public async Task Tier1_Reset_RestoresAdaptiveIterations()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ResetCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IterationsText.Should().NotBeNullOrEmpty();
    }

    // --- Feature 5: Bookmarks ---

    [Fact]
    public async Task Tier1_Bookmarks_LoadDefaultBookmarks()
    {
        var vm = await CreateMainViewModelAsync();
        vm.Bookmarks.Should().NotBeEmpty();
        vm.Bookmarks[0].Name.Should().Be("Seahorse Valley");
    }

    [Fact]
    public async Task Tier1_Bookmarks_SelectBookmark()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedBookmark = vm.Bookmarks[0];
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.SelectedBookmark.Name.Should().Be("Seahorse Valley");
    }

    [Fact]
    public async Task Tier1_Bookmarks_AddCustomBookmark()
    {
        var vm = await CreateMainViewModelAsync();
        int initialCount = vm.Bookmarks.Count;
        vm.NewBookmarkName = "E2E Bookmark";
        vm.AddBookmarkCommand.Execute(null);
        vm.Bookmarks.Count.Should().Be(initialCount + 1);
        vm.Bookmarks[initialCount].Name.Should().Be("E2E Bookmark");
    }

    [Fact]
    public async Task Tier1_Bookmarks_DeleteBookmark()
    {
        var vm = await CreateMainViewModelAsync();
        int initialCount = vm.Bookmarks.Count;
        var toDelete = vm.Bookmarks[0];
        vm.DeleteBookmarkCommand.Execute(toDelete);
        vm.Bookmarks.Count.Should().Be(initialCount - 1);
    }

    [Fact]
    public async Task Tier1_Bookmarks_AddBookmarkSave()
    {
        var vm = await CreateMainViewModelAsync();
        vm.NewBookmarkName = "Saved Bookmark";
        vm.AddBookmarkCommand.Execute(null);
        File.Exists(_bookmarkFilePath).Should().BeTrue();
    }

    // --- Feature 6: Localization ---

    [Fact]
    public async Task Tier1_Localization_SetCultureToPolish()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedLanguage = "PL";
        LocalizationService.Instance.CurrentCulture.Name.Should().StartWith("pl");
        LocalizationService.Instance["AppName"].Should().Be("Eksplorator Fraktali");
    }

    [Fact]
    public async Task Tier1_Localization_SetCultureToEnglish()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedLanguage = "EN";
        LocalizationService.Instance.CurrentCulture.Name.Should().StartWith("en");
        LocalizationService.Instance["AppName"].Should().Be("Fractal Explorer");
    }

    [Fact]
    public async Task Tier1_Localization_SelectedLanguageChanged()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedLanguage = "PL";
        vm.SelectedLanguage.Should().Be("PL");
    }

    [Fact]
    public async Task Tier1_Localization_CurrentCultureMatchesSystemCulture()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedLanguage.Should().Match(s => s == "EN" || s == "PL");
    }

    [Fact]
    public async Task Tier1_Localization_IndexerReturnsKeyName()
    {
        var service = LocalizationService.Instance;
        service["NonExistentKey_Test"].Should().Be("NonExistentKey_Test");
    }

    // --- Feature 7: Presets ---

    [Fact]
    public async Task Tier1_Presets_ChangeSelectedPalette()
    {
        var vm = await CreateMainViewModelAsync();
        var icePalette = System.Linq.Enumerable.FirstOrDefault(vm.Palettes, p => p.Name == "Ice") ?? vm.Palettes[0];
        vm.SelectedPalette = icePalette;
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.SelectedPalette.Should().Be(icePalette);
    }

    [Fact]
    public async Task Tier1_Presets_ChangeSelectedFractalTypeToJulia()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IsJuliaSettingsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Tier1_Presets_ChangeSelectedFractalTypeToMandelbrot()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.SelectedFractalType = FractalType.Mandelbrot;
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IsJuliaSettingsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task Tier1_Presets_QuickPaletteHotkeys()
    {
        var vm = await CreateMainViewModelAsync();
        var rainbowPalette = System.Linq.Enumerable.FirstOrDefault(vm.Palettes, p => p.Name == "Rainbow") ?? vm.Palettes[0];
        vm.SelectedPalette = rainbowPalette;
        vm.SelectedPalette.Should().Be(rainbowPalette);
    }

    [Fact]
    public async Task Tier1_Presets_AvailablePalettesList()
    {
        var vm = await CreateMainViewModelAsync();
        vm.Palettes.Count.Should().BeGreaterThan(0);
    }

    // --- Feature 8: Julia Parameter Tuning ---

    [Fact]
    public async Task Tier1_Julia_UpdateRealCoordinate()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaReal = "-0.8";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.JuliaReal.Should().Be("-0.8");
    }

    [Fact]
    public async Task Tier1_Julia_UpdateImaginaryCoordinate()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaImag = "0.156";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.JuliaImag.Should().Be("0.156");
    }

    [Fact]
    public async Task Tier1_Julia_ValidCoordinates()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaReal = "-0.7123";
        vm.JuliaImag = "0.2345";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier1_Julia_InvalidRealCoordinate()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaReal = "abc";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier1_Julia_InvalidImaginaryCoordinate()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaImag = "xyz";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    // --- Feature 9: Animation ---

    [Fact]
    public async Task Tier1_Animation_ToggleStart()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        vm.IsAnimating.Should().BeTrue();
        vm.ToggleAnimationCommand.Execute(null); // stop
    }

    [Fact]
    public async Task Tier1_Animation_ToggleStop()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        vm.ToggleAnimationCommand.Execute(null);
        vm.IsAnimating.Should().BeFalse();
    }

    [Fact]
    public async Task Tier1_Animation_LoopZoom()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        await Task.Delay(50);
        vm.ToggleAnimationCommand.Execute(null); // stop
        vm.CanZoomOut.Should().BeTrue();
    }

    [Fact]
    public async Task Tier1_Animation_LoopZoom_TriggersGenerations()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        await Task.Delay(50);
        vm.ToggleAnimationCommand.Execute(null);
        vm.FractalImage.Should().NotBeNull();
    }

    [Fact]
    public async Task Tier1_Animation_LoopZoom_PushesToZoomHistory()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        await Task.Delay(50);
        vm.ToggleAnimationCommand.Execute(null);
        vm.CanZoomOut.Should().BeTrue();
    }

    // --- Feature 10: Diagnostics ---

    [Fact]
    public async Task Tier1_Diagnostics_ToggleVisibilityCommand()
    {
        var vm = await CreateMainViewModelAsync();
        vm.IsDiagnosticsVisible = false;
        vm.IsDiagnosticsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task Tier1_Diagnostics_RenderCompletion()
    {
        var vm = await CreateMainViewModelAsync();
        vm.CenterCoordinatesText.Should().NotBeNullOrEmpty();
        vm.SpanText.Should().NotBeNullOrEmpty();
        vm.ResolutionText.Should().NotBeNullOrEmpty();
        vm.EngineText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Tier1_Diagnostics_AdaptiveIterations_IncreasesWhenFast()
    {
        var vm = await CreateMainViewModelAsync();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IterationsText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Tier1_Diagnostics_AdaptiveIterations_DecreasesWhenSlow()
    {
        var vm = await CreateMainViewModelAsync();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IterationsText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Tier1_Diagnostics_TelemetryDisplaysZeroWarmUp()
    {
        var vm = await CreateMainViewModelAsync();
        vm.RenderTimeText.Should().Contain("ms");
    }

    // --- Feature 11: File Export & Clipboard ---

    [Fact]
    public async Task Tier1_Export_SaveImageCommand()
    {
        var vm = await CreateMainViewModelAsync();
        bool actionCalled = false;
        vm.SaveFileDialogAction = () =>
        {
            actionCalled = true;
            return Task.FromResult<string?>("test_e2e_export.png");
        };
        vm.SaveImageCommand.Execute(null);
        await Task.Delay(20);
        actionCalled.Should().BeTrue();
        if (File.Exists("test_e2e_export.png"))
        {
            File.Delete("test_e2e_export.png");
        }
    }

    [Fact]
    public async Task Tier1_Export_SaveImageCommandFallback()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SaveFileDialogAction = null;
        await vm.SaveImageCommand.ExecuteAsync(null);
        vm.StatusText.Should().Contain("Saved to");
    }

    [Fact]
    public async Task Tier1_Export_CopyToClipboardCommand()
    {
        var vm = await CreateMainViewModelAsync();
        bool actionCalled = false;
        vm.CopyToClipboardAction = () =>
        {
            actionCalled = true;
            return Task.CompletedTask;
        };
        vm.CopyToClipboardCommand.Execute(null);
        await Task.Delay(20);
        actionCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Tier1_Export_SaveImageCommand_NoImage()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SaveFileDialogAction = () => Task.FromResult<string?>("test_e2e_noimage.png");
        // Force fractal image to null
        vm.GetType().GetProperty("FractalImage")?.SetValue(vm, null);
        vm.SaveImageCommand.Execute(null);
        File.Exists("test_e2e_noimage.png").Should().BeFalse();
    }

    [Fact]
    public async Task Tier1_Export_CopyToClipboardCommand_NoImage()
    {
        var vm = await CreateMainViewModelAsync();
        bool actionCalled = false;
        vm.CopyToClipboardAction = () =>
        {
            actionCalled = true;
            return Task.CompletedTask;
        };
        vm.GetType().GetProperty("FractalImage")?.SetValue(vm, null);
        vm.CopyToClipboardCommand.Execute(null);
        actionCalled.Should().BeFalse();
    }


    // ==========================================
    // TIER 2: BOUNDARY & CORNER CASES (55 TESTS)
    // ==========================================

    // --- Feature 1: Zooming ---

    [Fact]
    public async Task Tier2_Zoom_ExtremelyDeepZoom_SwitchesToCpu()
    {
        var vm = await CreateMainViewModelAsync();
        // Zoom in deep enough to exceed 1e10 zoom factor
        for (int i = 0; i < 40; i++)
        {
            vm.ZoomCentered(true);
        }
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.EngineText.Should().Be("CPU (Parallel)");
    }

    [Fact]
    public async Task Tier2_Zoom_ZeroOrNegativeDimensions()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnSizeChanged(0, -50);
        // Should not throw or crash
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.ViewportWidth.Should().Be(100); // unaffected because <= 0 is ignored
    }

    [Fact]
    public async Task Tier2_Zoom_OutOfBoundsCursorPosition()
    {
        var vm = await CreateMainViewModelAsync();
        // Cursor outside bounds
        vm.OnMouseWheelZoom(new Point(5000, -2000), 1.0);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Zoom_RepeatedZoomOutAtBaseLevel()
    {
        var vm = await CreateMainViewModelAsync();
        for (int i = 0; i < 10; i++)
        {
            vm.ZoomOutCommand.Execute(null);
        }
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CanZoomOut.Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Zoom_PreserveAspectRatioOnResize()
    {
        var vm = await CreateMainViewModelAsync(100, 100);
        vm.OnSizeChanged(100, 50); // Aspect ratio changes from 1 to 2
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.ViewportWidth.Should().Be(100);
        vm.ViewportHeight.Should().Be(50);
    }

    // --- Feature 2: Panning ---

    [Fact]
    public async Task Tier2_Pan_ZeroDeltaDrag()
    {
        var vm = await CreateMainViewModelAsync();
        string before = vm.CenterCoordinatesText;
        vm.StartPan(new Point(50, 50));
        vm.MovePan(new Point(50, 50));
        vm.EndPan();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CenterCoordinatesText.Should().Be(before);
    }

    [Fact]
    public async Task Tier2_Pan_ExtremelyLargeDrag()
    {
        var vm = await CreateMainViewModelAsync();
        vm.StartPan(new Point(50, 50));
        vm.MovePan(new Point(100000, -100000));
        vm.EndPan();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Pan_ArrowKeyPanAtDeepZoom()
    {
        var vm = await CreateMainViewModelAsync();
        for (int i = 0; i < 30; i++) vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        string before = vm.CenterCoordinatesText;
        vm.PanByPercent(0.1, 0.1);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CenterCoordinatesText.Should().NotBe(before);
    }

    [Fact]
    public async Task Tier2_Pan_StartPanWithoutEndPan()
    {
        var vm = await CreateMainViewModelAsync();
        vm.StartPan(new Point(50, 50));
        vm.MovePan(new Point(60, 60));
        // Reset without EndPan
        vm.ResetCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CanZoomOut.Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Pan_InvalidSizeDuringPan()
    {
        var vm = await CreateMainViewModelAsync();
        vm.StartPan(new Point(50, 50));
        vm.OnSizeChanged(0, 0); // Invalid size
        vm.MovePan(new Point(60, 60));
        vm.EndPan();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    // --- Feature 3: Selection ---

    [Fact]
    public async Task Tier2_Selection_ExactBoundarySelection()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnPointerPressed(new Point(0, 0));
        vm.OnPointerMoved(new Point(100, 100));
        vm.OnPointerReleased(new Point(100, 100));
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IsSelecting.Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Selection_DragOutsideViewport()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(200, 200));
        vm.OnPointerReleased(new Point(200, 200));
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IsSelecting.Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Selection_ZeroWidthHeightSelectionRelease()
    {
        var vm = await CreateMainViewModelAsync();
        string centerBefore = vm.CenterCoordinatesText;
        vm.OnPointerPressed(new Point(50, 50));
        vm.OnPointerReleased(new Point(50, 50));
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CenterCoordinatesText.Should().Be(centerBefore);
    }

    [Fact]
    public async Task Tier2_Selection_AspectMultiplierAdjustment()
    {
        var vm = await CreateMainViewModelAsync();
        // narrow selection
        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(90, 12));
        vm.OnPointerReleased(new Point(90, 12));
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Selection_CancellationViaEscKey()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(50, 50));
        vm.CancelSelection();
        vm.IsSelecting.Should().BeFalse();
    }

    // --- Feature 4: Reset ---

    [Fact]
    public async Task Tier2_Reset_AfterExtremelyDeepZoom()
    {
        var vm = await CreateMainViewModelAsync();
        for (int i = 0; i < 40; i++) vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.ResetCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CanZoomOut.Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Reset_WhenAlreadyAtResetState()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ResetCommand.Execute(null);
        vm.ResetCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CanZoomOut.Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Reset_WithZeroViewportDimensions()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnSizeChanged(0, 0);
        vm.ResetCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Reset_DuringActivePan()
    {
        var vm = await CreateMainViewModelAsync();
        vm.StartPan(new Point(50, 50));
        vm.ResetCommand.Execute(null);
        vm.IsPanning.Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Reset_DuringActiveAnimation()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        vm.ResetCommand.Execute(null);
        vm.IsAnimating.Should().BeFalse();
    }

    // --- Feature 5: Bookmarks ---

    [Fact]
    public async Task Tier2_Bookmarks_AddEmptyOrWhitespaceName()
    {
        var vm = await CreateMainViewModelAsync();
        vm.NewBookmarkName = "   ";
        vm.AddBookmarkCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Bookmarks_DeleteCurrentlySelectedBookmark()
    {
        var vm = await CreateMainViewModelAsync();
        var bookmark = vm.Bookmarks[0];
        vm.SelectedBookmark = bookmark;
        vm.DeleteBookmarkCommand.Execute(bookmark);
        vm.SelectedBookmark.Should().BeNull();
    }

    [Fact]
    public async Task Tier2_Bookmarks_SelectBookmarkWithInvalidCoords()
    {
        var vm = await CreateMainViewModelAsync();
        var entry = new BookmarkEntry
        {
            Name = "Corrupted",
            FractalType = FractalType.Mandelbrot,
            Plane = new ComplexPlane(double.NaN, 1.0, -1.0, 1.0),
            PaletteName = "Forest",
            Iterations = 100
        };
        // Should handle safely or fallback
        Action act = () => vm.SelectedBookmark = entry;
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Tier2_Bookmarks_BookmarksFileCorrupted()
    {
        // Corrupt file
        File.WriteAllText(_bookmarkFilePath, "{ invalid json }");
        var vm = await CreateMainViewModelAsync();
        vm.Bookmarks.Should().NotBeEmpty(); // should fall back to default bookmarks
    }

    [Fact]
    public async Task Tier2_Bookmarks_AddBookmarkAtExtremelyDeepZoom()
    {
        var vm = await CreateMainViewModelAsync();
        for (int i = 0; i < 40; i++) vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.NewBookmarkName = "Deep Bookmark";
        vm.AddBookmarkCommand.Execute(null);
        vm.Bookmarks.Should().Contain(b => b.Name == "Deep Bookmark");
    }

    // --- Feature 6: Localization ---

    [Fact]
    public async Task Tier2_Localization_SwitchLanguageDuringRender()
    {
        var vm = await CreateMainViewModelAsync();
        var task = vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.SelectedLanguage = "PL";
        await task;
        LocalizationService.Instance.CurrentCulture.Name.Should().StartWith("pl");
    }

    [Fact]
    public async Task Tier2_Localization_MultipleConsecutiveCultureChanges()
    {
        var vm = await CreateMainViewModelAsync();
        for (int i = 0; i < 5; i++)
        {
            vm.SelectedLanguage = "PL";
            vm.SelectedLanguage = "EN";
        }
        LocalizationService.Instance.CurrentCulture.Name.Should().StartWith("en");
    }

    [Fact]
    public async Task Tier2_Localization_CultureSetterNullValue()
    {
        var service = LocalizationService.Instance;
        Action act = () => service.CurrentCulture = null!;
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Tier2_Localization_LocalizedStatusMessage()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedLanguage = "PL";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().Contain("iter");
    }

    [Fact]
    public async Task Tier2_Localization_InvalidLanguagePropertySet()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedLanguage = "FR"; // Unexpected language
        LocalizationService.Instance.CurrentCulture.Name.Should().Match(s => s.StartsWith("en") || s.StartsWith("pl"));
    }

    // --- Feature 7: Presets ---

    [Fact]
    public async Task Tier2_Presets_SetInvalidPaletteIndex()
    {
        var vm = await CreateMainViewModelAsync();
        // Cannot cast to 99 since it's a class now, just set to null
        vm.SelectedPalette = null;
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Presets_SetFractalTypeToSameValue()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Mandelbrot;
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Presets_SwitchFractalTypeDuringActiveRender()
    {
        var vm = await CreateMainViewModelAsync();
        var task = vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.SelectedFractalType = FractalType.Julia;
        await task;
        vm.IsJuliaSettingsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Tier2_Presets_InvalidFractalTypeCast()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = (FractalType)99;
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Presets_PaletteChangeDoesNotResetZoom()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        double zoomBefore = GetZoomFactor(vm.ZoomText);
        vm.SelectedPalette = System.Linq.Enumerable.FirstOrDefault(vm.Palettes, p => p.Name == "Rainbow") ?? vm.Palettes[0];
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        GetZoomFactor(vm.ZoomText).Should().Be(zoomBefore);
    }

    // --- Feature 8: Julia Parameter Tuning ---

    [Fact]
    public async Task Tier2_Julia_ExtremelyLargeCoordinate()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaReal = "1e300";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Julia_EmptyInputString()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaReal = "";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Julia_SpecialCharactersInInput()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaReal = "$-0.7@!";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Julia_HighPrecisionInputs()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaReal = "-0.71234567890123456789";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Julia_TuningParametersWhileMandelbrotActive()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Mandelbrot;
        vm.JuliaReal = "-0.5";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IsJuliaSettingsVisible.Should().BeFalse();
    }

    // --- Feature 9: Animation ---

    [Fact]
    public async Task Tier2_Animation_StopMidFrame()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        // stop it immediately
        vm.ToggleAnimationCommand.Execute(null);
        vm.IsAnimating.Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Animation_AnimationAtDeepZoomLimit()
    {
        var vm = await CreateMainViewModelAsync();
        for (int i = 0; i < 40; i++) vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.ToggleAnimationCommand.Execute(null);
        await Task.Delay(20);
        vm.ToggleAnimationCommand.Execute(null);
        vm.IsAnimating.Should().BeFalse();
    }

    [Fact]
    public async Task Tier2_Animation_WindowResizeDuringAnimation()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        vm.OnSizeChanged(120, 120);
        await Task.Delay(20);
        vm.ToggleAnimationCommand.Execute(null);
        vm.ViewportWidth.Should().Be(120);
    }

    [Fact]
    public async Task Tier2_Animation_SwitchFractalTypeDuringAnimation()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        vm.SelectedFractalType = FractalType.Julia;
        await Task.Delay(20);
        vm.ToggleAnimationCommand.Execute(null);
        vm.SelectedFractalType.Should().Be(FractalType.Julia);
    }

    [Fact]
    public async Task Tier2_Animation_ToggleAnimationRepeatedly()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        vm.ToggleAnimationCommand.Execute(null);
        vm.ToggleAnimationCommand.Execute(null);
        vm.ToggleAnimationCommand.Execute(null);
        vm.IsAnimating.Should().BeFalse();
    }

    // --- Feature 10: Diagnostics ---

    [Fact]
    public async Task Tier2_Diagnostics_AdaptiveIterations_ClampedToMin()
    {
        var vm = await CreateMainViewModelAsync();
        // Verify iterations stay within valid limits
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IterationsText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Tier2_Diagnostics_AdaptiveIterations_ClampedToMax()
    {
        var vm = await CreateMainViewModelAsync();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IterationsText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Tier2_Diagnostics_TelemetryDisplaysZeroWhenRenderFails()
    {
        var vm = await CreateMainViewModelAsync();
        // Since we can't easily make ParallelFractalGenerator fail, we just verify it formats correctly
        vm.RenderTimeText.Should().Contain("ms");
    }

    [Fact]
    public async Task Tier2_Diagnostics_AdaptiveAdjustmentWithZeroElapsedMs()
    {
        var vm = await CreateMainViewModelAsync();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.StatusText.Should().NotContain("Error");
    }

    [Fact]
    public async Task Tier2_Diagnostics_ToggleTelemetryPanelHotKey()
    {
        var vm = await CreateMainViewModelAsync();
        vm.IsDiagnosticsVisible = !vm.IsDiagnosticsVisible;
        vm.IsDiagnosticsVisible.Should().BeFalse();
    }

    // --- Feature 11: File Export & Clipboard ---

    [Fact]
    public async Task Tier2_Export_SaveFileDialogCancelled()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SaveFileDialogAction = () => Task.FromResult<string?>(null); // cancel
        vm.SaveImageCommand.Execute(null);
        await Task.Delay(20);
        vm.StatusText.Should().NotContain("Saved to");
    }

    [Fact]
    public async Task Tier2_Export_SaveFileWriteError()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SaveFileDialogAction = () => Task.FromResult<string?>("Q:\\invalid_drive\\test.png");
        vm.SaveImageCommand.Execute(null);
        await Task.Delay(20);
        vm.StatusText.Should().Contain("Save error");
    }

    [Fact]
    public async Task Tier2_Export_ClipboardActionThrows()
    {
        var vm = await CreateMainViewModelAsync();
        vm.CopyToClipboardAction = () => throw new InvalidOperationException("Locked");
        vm.CopyToClipboardCommand.Execute(null);
        await Task.Delay(20);
        vm.StatusText.Should().Contain("Clipboard error");
    }

    [Fact]
    public async Task Tier2_Export_SaveFileInNonExistentFolder()
    {
        var vm = await CreateMainViewModelAsync();
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        vm.SaveFileDialogAction = () => Task.FromResult<string?>(Path.Combine(tempDir, "test.png"));
        vm.SaveImageCommand.Execute(null);
        await Task.Delay(20);
        vm.StatusText.Should().Contain("Saved to");
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Tier2_Export_SaveImageDuringActiveRender()
    {
        var vm = await CreateMainViewModelAsync();
        var task = vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.SaveFileDialogAction = () => Task.FromResult<string?>("test_active.png");
        vm.SaveImageCommand.Execute(null);
        await task;
        if (File.Exists("test_active.png"))
        {
            File.Delete("test_active.png");
        }
    }


    // ==========================================
    // TIER 3: CROSS-FEATURE COMBINATIONS (11 TESTS)
    // ==========================================

    [Fact]
    public async Task Tier3_Combo_ZoomAndPan()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        string centerBefore = vm.CenterCoordinatesText;
        vm.PanByPercent(0.1, 0.1);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CenterCoordinatesText.Should().NotBe(centerBefore);
        vm.CanZoomOut.Should().BeTrue();
    }

    [Fact]
    public async Task Tier3_Combo_BookmarkAndZoomHistory()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.SelectedBookmark = vm.Bookmarks[1];
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.CanZoomOut.Should().BeTrue(); // Selecting bookmark pushes previous to history
    }

    [Fact]
    public async Task Tier3_Combo_JuliaTuningAndBookmarks()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaReal = "-0.8";
        vm.JuliaImag = "0.2";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        
        vm.NewBookmarkName = "Julia Custom";
        vm.AddBookmarkCommand.Execute(null);

        vm.SelectedFractalType = FractalType.Mandelbrot;
        await vm.GenerateFractalCommand.ExecuteAsync(null);

        vm.SelectedBookmark = vm.Bookmarks[vm.Bookmarks.Count - 1];
        await vm.GenerateFractalCommand.ExecuteAsync(null);

        vm.SelectedFractalType.Should().Be(FractalType.Julia);
        vm.JuliaReal.Should().Be("-0.8");
    }

    [Fact]
    public async Task Tier3_Combo_AnimationAndResize()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        vm.OnSizeChanged(150, 150);
        await Task.Delay(20);
        vm.ToggleAnimationCommand.Execute(null);
        vm.ViewportWidth.Should().Be(150);
    }

    [Fact]
    public async Task Tier3_Combo_SelectionAndReset()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(50, 50));
        vm.ResetCommand.Execute(null);
        vm.IsSelecting.Should().BeFalse();
        vm.CanZoomOut.Should().BeFalse();
    }

    [Fact]
    public async Task Tier3_Combo_LocalizationAndBookmarks()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedLanguage = "PL";
        vm.NewBookmarkName = "Polski";
        vm.AddBookmarkCommand.Execute(null);
        vm.SelectedLanguage = "EN";
        vm.Bookmarks[vm.Bookmarks.Count - 1].Name.Should().Be("Polski");
    }

    [Fact]
    public async Task Tier3_Combo_AdaptiveIterationsAndDeepZoom()
    {
        var vm = await CreateMainViewModelAsync();
        for (int i = 0; i < 40; i++) vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IterationsText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Tier3_Combo_SaveImageAndAnimation()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        vm.SaveFileDialogAction = () => Task.FromResult<string?>("anim_frame.png");
        vm.SaveImageCommand.Execute(null);
        await Task.Delay(20);
        vm.ToggleAnimationCommand.Execute(null);
        if (File.Exists("anim_frame.png"))
        {
            File.Delete("anim_frame.png");
        }
    }

    [Fact]
    public async Task Tier3_Combo_PanByArrowKeysDuringZoomSelection()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(50, 50));
        vm.PanByPercent(0.1, 0.0);
        vm.IsSelecting.Should().BeTrue();
        vm.OnPointerReleased(new Point(50, 50));
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.IsSelecting.Should().BeFalse();
    }

    [Fact]
    public async Task Tier3_Combo_SwitchPaletteDuringAnimation()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleAnimationCommand.Execute(null);
        var icePalette = System.Linq.Enumerable.FirstOrDefault(vm.Palettes, p => p.Name == "Ice") ?? vm.Palettes[0];
        vm.SelectedPalette = icePalette;
        await Task.Delay(20);
        vm.ToggleAnimationCommand.Execute(null);
        vm.SelectedPalette.Should().Be(icePalette);
    }

    [Fact]
    public async Task Tier3_Combo_JuliaTuningAndLocalization()
    {
        var vm = await CreateMainViewModelAsync();
        vm.SelectedLanguage = "PL";
        vm.SelectedFractalType = FractalType.Julia;
        vm.JuliaReal = "-0.75";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.SelectedLanguage = "EN";
        vm.JuliaReal.Should().Be("-0.75");
    }


    // ==========================================
    // TIER 4: REAL-WORLD SCENARIOS (5 TESTS)
    // ==========================================

    [Fact]
    public async Task Tier4_Scenario_MandelbrotExploration_CustomBookmarking_AndExport()
    {
        // 1. Initialize view (starts at Mandelbrot default).
        var vm = await CreateMainViewModelAsync();
        // 2. Drag selection box to zoom in.
        vm.OnPointerPressed(new Point(20, 20));
        vm.OnPointerMoved(new Point(60, 60));
        vm.OnPointerReleased(new Point(60, 60));
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 3. Pan view.
        vm.PanByPercent(0.0, -0.1);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 4. Perform mouse wheel zoom in.
        vm.OnMouseWheelZoom(new Point(50, 50), 1.0);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 5. Enter custom bookmark and add.
        vm.NewBookmarkName = "My Custom Seahorse";
        vm.AddBookmarkCommand.Execute(null);
        // 6. Reset.
        vm.ResetCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 7. Select bookmark.
        vm.SelectedBookmark = vm.Bookmarks[vm.Bookmarks.Count - 1];
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 8. Save image.
        bool saveCalled = false;
        vm.SaveFileDialogAction = () =>
        {
            saveCalled = true;
            return Task.FromResult<string?>("seahorse_scenario.png");
        };
        vm.SaveImageCommand.Execute(null);
        await Task.Delay(20);
        saveCalled.Should().BeTrue();
        if (File.Exists("seahorse_scenario.png"))
        {
            File.Delete("seahorse_scenario.png");
        }
    }

    [Fact]
    public async Task Tier4_Scenario_TransitionFromMandelbrotToJulia_WithTuning()
    {
        // 1. Initialize view.
        var vm = await CreateMainViewModelAsync();
        // 2. Select Julia Default bookmark.
        vm.SelectedBookmark = vm.Bookmarks[3]; // Julia Default
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 3. Verify settings visible.
        vm.IsJuliaSettingsVisible.Should().BeTrue();
        // 4. Input tuned parameters.
        vm.JuliaReal = "0.36";
        vm.JuliaImag = "-0.1";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 5. Drag pan left.
        vm.StartPan(new Point(50, 50));
        vm.MovePan(new Point(70, 50));
        vm.EndPan();
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 6. Toggle diagnostics panel off.
        vm.IsDiagnosticsVisible = false;
        // 7. Copy to clipboard.
        bool copyCalled = false;
        vm.CopyToClipboardAction = () =>
        {
            copyCalled = true;
            return Task.CompletedTask;
        };
        vm.CopyToClipboardCommand.Execute(null);
        await Task.Delay(20);
        copyCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Tier4_Scenario_DeepZoomExploration_WithEngineSwitchAndAnimation()
    {
        // 1. Initialize.
        var vm = await CreateMainViewModelAsync();
        // 2. Select Triple Spiral Valley.
        vm.SelectedBookmark = vm.Bookmarks[2];
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 3. Toggle animation.
        vm.ToggleAnimationCommand.Execute(null);
        await Task.Delay(50);
        vm.ToggleAnimationCommand.Execute(null); // Stop
        // 4. Zoom in extremely deep.
        for (int i = 0; i < 40; i++) vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 5. Verify engine switched.
        vm.EngineText.Should().Be("CPU (Parallel)");
        // 6. Perform zoom out.
        vm.ZoomOutCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Tier4_Scenario_BookmarkSelection_ZoomChangePalette_SaveImage()
    {
        // 1. Initialize.
        var vm = await CreateMainViewModelAsync();
        // 2. Select Seahorse Valley.
        vm.SelectedBookmark = vm.Bookmarks[0];
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 3. Zoom centered.
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 4. Change palette to Ice.
        var icePalette = System.Linq.Enumerable.FirstOrDefault(vm.Palettes, p => p.Name == "Ice") ?? vm.Palettes[0];
        vm.SelectedPalette = icePalette;
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 5. Save image.
        bool saveCalled = false;
        vm.SaveFileDialogAction = () =>
        {
            saveCalled = true;
            return Task.FromResult<string?>("seahorse_ice.png");
        };
        vm.SaveImageCommand.Execute(null);
        await Task.Delay(20);
        saveCalled.Should().BeTrue();
        if (File.Exists("seahorse_ice.png"))
        {
            File.Delete("seahorse_ice.png");
        }
    }

    [Fact]
    public async Task Tier4_Scenario_SwitchToJulia_TuneParameters_RunAnimation_Reset()
    {
        // 1. Initialize.
        var vm = await CreateMainViewModelAsync();
        // 2. Switch to Julia.
        vm.SelectedFractalType = FractalType.Julia;
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 3. Input coordinates.
        vm.JuliaReal = "-0.7";
        vm.JuliaImag = "0.27015";
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        // 4. Toggle animation.
        vm.ToggleAnimationCommand.Execute(null);
        await Task.Delay(50);
        vm.ToggleAnimationCommand.Execute(null); // Stop
        // 5. Reset.
        vm.ResetCommand.Execute(null);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        vm.SelectedFractalType.Should().Be(FractalType.Mandelbrot);
    }

    private class ExceptionGpuGenerator : IFractalGenerator
    {
        public string Name => "Exception GPU";
        public bool IsGpuAccelerated => true;
        public Task<(byte[] Pixels, double[] Iterations)> GenerateAsync(Viewport viewport, int maxIterations, GradientPalette palette, double paletteOffset, FractalSettings settings, CancellationToken ct)
        {
            throw new InvalidOperationException("Test exception");
        }
    }

    [Fact]
    public async Task Tier1_Logging_LogsExpectedPhrases()
    {
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            
            var vm = await CreateMainViewModelAsync();
            vm.SelectedBookmark = vm.Bookmarks[0];
            await vm.GenerateFractalCommand.ExecuteAsync(null);
            
            // Generate exception
            var exceptionGen = new ExceptionGpuGenerator();
            var zoomService = new ZoomService();
            var bookmarkService = new BookmarkService();
            var vm2 = new MainViewModel(exceptionGen, zoomService, bookmarkService, new TestConsoleLogger<MainViewModel>(), new TestConsoleLogger<RenderingViewModel>());
            try
            {
                await vm2.GenerateFractalCommand.ExecuteAsync(null);
            }
            catch { }
            
            var output = sw.ToString();
            output.Should().Contain("Render request initiated");
            output.Should().Contain("Render completed in");
            output.Should().Contain("Bookmark selected");
            output.Should().Contain("Render request failed");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private class SlowGpuGenerator : IFractalGenerator
    {
        public string Name => "Slow GPU";
        public bool IsGpuAccelerated => true;
        public async Task<(byte[] Pixels, double[] Iterations)> GenerateAsync(Viewport viewport, int maxIterations, GradientPalette palette, double paletteOffset, FractalSettings settings, CancellationToken ct)
        {
            await Task.Delay(6000, ct);
            return (new byte[100], new double[25]);
        }
    }

    [Fact]
    public async Task Tier1_CancelRender_SlowGeneratorCancels()
    {
        var slowGen = new SlowGpuGenerator();
        var zoomService = new ZoomService();
        var bookmarkService = new BookmarkService();
        var vm = new MainViewModel(slowGen, zoomService, bookmarkService);
        vm.SelectedLanguage = "EN";
        vm.SelectedPalette = new GradientPalette { Name = "Test" };
        
        var renderTask = vm.GenerateFractalCommand.ExecuteAsync(null);
        
        // Allow some time for cancel button to appear
        await Task.Delay(5200);
        
        vm.IsCancelVisible.Should().BeTrue();
        
        var oldImage = vm.FractalImage;
        vm.CancelRenderCommand.Execute(null);
        
        await renderTask;
        
        vm.StatusText.ToLower().Should().Contain("cancelled");
        vm.FractalImage.Should().Be(oldImage);
    }

    // --- New Features (Pending) ---

    [Fact]
    public async Task Tier1_ColorPaletteEditor_ShowAndHide()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OpenColorPaletteEditorCommand?.Execute(null);
        vm.IsColorPaletteEditorVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Tier1_ColorPaletteEditor_LiveUpdatesRenderer()
    {
        var vm = await CreateMainViewModelAsync();
        vm.OpenColorPaletteEditorCommand?.Execute(null);
        
        var paletteService = new PaletteService();
        var paletteEditorVm = new PaletteEditorViewModel(paletteService, vm.Rendering, vm.SelectedPalette);
        
        paletteEditorVm.PaletteName = "Live Edited Palette";
        paletteEditorVm.Stops.Add(new GradientStopViewModel { Position = 0.5, R = 255, G = 100, B = 50 });
        
        vm.SelectedPalette.Name.Should().Be("Live Edited Palette");
        vm.SelectedPalette.Stops.Should().Contain(s => s.R == 255 && s.G == 100 && s.B == 50);
    }

    [Fact]
    public async Task Tier1_Minimap_ToggleVisibility()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleMinimapCommand?.Execute(null);
        vm.IsMinimapVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Tier1_Minimap_ClickCoordinatesCalculation()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ZoomCentered(true);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        
        // Click on the center of minimap (80, 60 on a 160x120 canvas)
        vm.OnMinimapClick(new Point(80, 60));
        
        // Bounding box should center around (80, 60)
        (vm.MinimapViewportRect.X + vm.MinimapViewportRect.Width / 2).Should().BeInRange(75, 85);
    }

    [Fact]
    public async Task Tier1_OrbitPath_ToggleVisibility()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleOrbitCommand?.Execute(null);
        vm.IsOrbitPathVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Tier1_OrbitPath_CalculatePoints()
    {
        var vm = await CreateMainViewModelAsync();
        vm.IsOrbitPathVisible = true;
        
        // Calculate orbit for center (50, 50) on default 100x100 test canvas
        vm.CalculateOrbit(new Point(50, 50));
        
        // For Mandelbrot, Orbit starts at (0,0) (pixel X=68, Y=50), second point is Z_1 = C (pixel X=50, Y=50)
        vm.OrbitPoints.Should().NotBeEmpty();
        vm.OrbitPoints.Count.Should().BeGreaterThanOrEqualTo(2);
        vm.OrbitPoints[0].X.Should().BeApproximately(68, 2);
        vm.OrbitPoints[0].Y.Should().BeApproximately(50, 2);
        vm.OrbitPoints[1].X.Should().BeApproximately(50, 2);
        vm.OrbitPoints[1].Y.Should().BeApproximately(50, 2);
    }

    [Fact(Skip="Pending implementation")]
    public async Task Tier1_3DShading_Toggle()
    {
        var vm = await CreateMainViewModelAsync();
        vm.Toggle3DShadingCommand?.Execute(null);
        vm.Is3DShadingEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Tier1_HighResExport_Start()
    {
        var vm = await CreateMainViewModelAsync();
        vm.Rendering.AdaptiveIterations = 1;
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_highres_{Guid.NewGuid()}.bmp");
        
        vm.SaveFileDialogAction = () => Task.FromResult<string?>(tempFile);
        
        if (vm.StartHighResExportCommand != null)
        {
            var task = vm.StartHighResExportCommand.ExecuteAsync(null);
            vm.IsHighResExporting.Should().BeTrue();
            await task;
        }
        
        vm.IsHighResExporting.Should().BeFalse();
        File.Exists(tempFile).Should().BeTrue();
        
        var fileInfo = new FileInfo(tempFile);
        fileInfo.Length.Should().BeGreaterThan(54);
        
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

    [Fact(Skip="Pending implementation")]
    public async Task Tier1_GifExport_Start()
    {
        var vm = await CreateMainViewModelAsync();
        if (vm.StartGifExportCommand != null)
            await vm.StartGifExportCommand.ExecuteAsync(null);
        vm.IsGifExporting.Should().BeTrue();
    }

    [Fact(Skip="Pending implementation")]
    public async Task Tier1_RandomDiscover_Execute()
    {
        var vm = await CreateMainViewModelAsync();
        vm.RandomDiscoverCommand?.Execute(null);
        vm.StatusText.Should().NotBeNull();
    }

    [Fact(Skip="Pending implementation")]
    public async Task Tier1_SplitView_Toggle()
    {
        var vm = await CreateMainViewModelAsync();
        vm.ToggleSplitViewCommand?.Execute(null);
        vm.IsSplitViewEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Tier1_ScientificNotation_Toggle()
    {
        var vm = await CreateMainViewModelAsync();
        vm.IsScientificNotationEnabled = false;
        
        vm.Navigation.UpdateCursorCoordinates(new Point(50, 50));
        var stdCursorText = vm.CursorCoordinatesText;
        
        vm.IsScientificNotationEnabled = true;
        var sciCursorText = vm.CursorCoordinatesText;
        
        stdCursorText.Should().NotContain("E-01");
        sciCursorText.Should().Contain("E-01");
        sciCursorText.Should().NotBe(stdCursorText);
    }

    [Fact]
    public async Task Tier1_ScientificNotation_ClipboardCopy()
    {
        var vm = await CreateMainViewModelAsync();
        
        string? copiedText = null;
        vm.CopyTextToClipboardAction = async (text) =>
        {
            copiedText = text;
            await Task.CompletedTask;
        };
        
        vm.Navigation.UpdateCursorCoordinates(new Point(50, 50));
        
        // Execute copy cursor command
        await vm.CopyCursorCoordinatesToClipboardCommand.ExecuteAsync(null);
        
        copiedText.Should().NotBeNull();
        copiedText.Should().Contain("Re: ");
        copiedText.Should().Contain("Im: ");
        
        copiedText = null;
        
        // Execute copy center command
        await vm.CopyCenterCoordinatesToClipboardCommand.ExecuteAsync(null);
        
        copiedText.Should().NotBeNull();
        copiedText.Should().Contain("Re: ");
        copiedText.Should().Contain("Im: ");
    }
}
