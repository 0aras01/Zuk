using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Fractal.UI.ViewModels;
using Fractal.Compute;
using Moq;

namespace Fractal.Tests.UI;

public class MemoryManagementTests
{
    private class SimulatedGpuGenerator : IFractalGenerator
    {
        private readonly ParallelFractalGenerator _cpuGenerator = new();

        public string Name => "GPU (Simulated)";
        public bool IsGpuAccelerated => true;

        public Task<byte[]> GenerateAsync(Viewport viewport, int maxIterations, int paletteId, FractalSettings settings, CancellationToken ct)
        {
            return _cpuGenerator.GenerateAsync(viewport, maxIterations, paletteId, settings, ct);
        }
    }

    private async Task<MainViewModel> CreateMainViewModelAsync(int width = 100, int height = 100)
    {
        var gpuGen = new SimulatedGpuGenerator();
        var zoomService = new ZoomService();
        var bookmarkService = new BookmarkService();
        var vm = new MainViewModel(gpuGen, zoomService, bookmarkService);
        vm.OnSizeChanged(width, height);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task BufferReuse_SameDimensions_ReusesBitmapInstance()
    {
        // Arrange
        var vm = await CreateMainViewModelAsync(150, 150);
        var firstBitmap = vm.FractalImage;
        firstBitmap.Should().NotBeNull();

        // Act - Trigger another render request with the SAME dimensions
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        var secondBitmap = vm.FractalImage;

        // Assert - The WriteableBitmap instance must be reused (reference equal)
        secondBitmap.Should().BeSameAs(firstBitmap);
    }

    [Fact]
    public async Task BufferReuse_DifferentDimensions_AllocatesNewBitmapInstance()
    {
        // Arrange
        var vm = await CreateMainViewModelAsync(150, 150);
        var firstBitmap = vm.FractalImage;
        firstBitmap.Should().NotBeNull();

        // Act - Change the viewport size and render
        vm.OnSizeChanged(200, 200);
        await vm.GenerateFractalCommand.ExecuteAsync(null);
        var secondBitmap = vm.FractalImage;

        // Assert - A new WriteableBitmap must be allocated (different reference)
        secondBitmap.Should().NotBeSameAs(firstBitmap);
    }

    [Fact]
    public void ILGPUFractalGenerator_DisposesGPUResourcesCorrectly()
    {
        // Arrange
        ILGPUFractalGenerator? gpu = null;
        try
        {
            gpu = new ILGPUFractalGenerator();
        }
        catch
        {
            // Suppress/skip if no hardware device is available to initialize ILGPU
            return;
        }

        // Act & Assert
        Action act = () => gpu.Dispose();
        act.Should().NotThrow();
    }
}
