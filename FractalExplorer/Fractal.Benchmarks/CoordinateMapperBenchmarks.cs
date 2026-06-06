using BenchmarkDotNet.Attributes;
using Fractal.Core.Models;
using Fractal.Core.Services;

namespace Fractal.Benchmarks;

/// <summary>
/// Benchmarks for CoordinateMapper pixel↔complex conversions.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class CoordinateMapperBenchmarks
{
    private Viewport _viewport;

    [GlobalSetup]
    public void Setup()
    {
        _viewport = new Viewport(new ComplexPlane(-2.5, 1.0, -1.5, 1.5), 1920, 1080);
    }

    [Benchmark(Description = "PixelToComplex")]
    public (DoubleDouble real, DoubleDouble imag) PixelToComplex()
    {
        return CoordinateMapper.PixelToComplex(960, 540, _viewport);
    }

    [Benchmark(Description = "ComplexToPixel")]
    public (int x, int y) ComplexToPixel()
    {
        return CoordinateMapper.ComplexToPixel(-0.75, 0.0, _viewport);
    }
}

