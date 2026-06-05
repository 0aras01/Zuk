using BenchmarkDotNet.Attributes;
using Fractal.Core.Models;
using Fractal.Core.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Fractal.Benchmarks;

/// <summary>
/// Benchmarks for full fractal image generation using the CPU ParallelFractalGenerator.
/// Measures end-to-end rendering throughput at different resolutions and iteration depths.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class FractalGeneratorBenchmarks
{
    private ParallelFractalGenerator _generator = null!;
    private Viewport _viewport;

    [Params(200, 500)]
    public int MaxIterations { get; set; }

    [Params("800x600", "1920x1080")]
    public string Resolution { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        _generator = new ParallelFractalGenerator();

        var parts = Resolution.Split('x');
        int w = int.Parse(parts[0]);
        int h = int.Parse(parts[1]);
        var plane = ZoomService.AdjustAspectRatio(
            new ComplexPlane(-2.5, 1.0, -1.5, 1.5), w, h);
        _viewport = new Viewport(plane, w, h);
    }

    [Benchmark(Description = "CPU ParallelFractalGenerator")]
    public async Task<byte[]> GenerateCpu()
    {
        return await _generator.GenerateAsync(_viewport, MaxIterations, 1, default, CancellationToken.None);
    }
}

