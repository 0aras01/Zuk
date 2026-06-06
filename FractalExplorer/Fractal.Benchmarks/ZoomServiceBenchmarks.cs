using BenchmarkDotNet.Attributes;
using Fractal.Core.Models;
using Fractal.Core.Services;

namespace Fractal.Benchmarks;

/// <summary>
/// Benchmarks for ZoomService operations including aspect ratio adjustment.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class ZoomServiceBenchmarks
{
    private ZoomService _zoomService = null!;

    [GlobalSetup]
    public void Setup()
    {
        _zoomService = new ZoomService();
        _zoomService.Reset(1920, 1080);
    }

    [Benchmark(Description = "ZoomTo")]
    public void ZoomTo()
    {
        _zoomService.ZoomTo(new ComplexPlane(-1.0, 0.5, -0.5, 0.5), 1920, 1080);
    }

    [Benchmark(Description = "ZoomOut")]
    public void ZoomOut()
    {
        // Push one entry so ZoomOut has something to pop
        _zoomService.ZoomTo(new ComplexPlane(-1.0, 0.5, -0.5, 0.5), 1920, 1080);
        _zoomService.ZoomOut(1920, 1080);
    }

    [Benchmark(Description = "ResizeCurrent")]
    public void ResizeCurrent()
    {
        _zoomService.ResizeCurrent(1280, 720);
    }

    [Benchmark(Description = "AdjustAspectRatio")]
    public ComplexPlane AdjustAspectRatio()
    {
        return ZoomService.AdjustAspectRatio(new ComplexPlane(-2.5, 1.0, -1.5, 1.5), 1920, 1080);
    }

    [Benchmark(Description = "Reset")]
    public void Reset()
    {
        _zoomService.Reset(1920, 1080);
    }
}

