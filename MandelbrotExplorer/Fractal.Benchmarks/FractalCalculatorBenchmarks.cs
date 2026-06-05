using BenchmarkDotNet.Attributes;
using Fractal.Core.Models;
using Fractal.Core.Services;

namespace Fractal.Benchmarks;

/// <summary>
/// Benchmarks for the core Mandelbrot iteration computation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class FractalCalculatorBenchmarks
{
    [Params(100, 500, 1000)]
    public int MaxIterations { get; set; }

    /// <summary>
    /// A point deep inside the set — always hits maxIterations (worst case).
    /// </summary>
    [Benchmark(Description = "Inside set (0, 0)")]
    public int ComputeInsideSet()
    {
        return FractalCalculator.ComputeIterations(0.0, 0.0, MaxIterations);
    }

    /// <summary>
    /// A point clearly outside the set — escapes in very few iterations (best case).
    /// </summary>
    [Benchmark(Description = "Outside set (2, 2)")]
    public int ComputeOutsideSet()
    {
        return FractalCalculator.ComputeIterations(2.0, 2.0, MaxIterations);
    }

    /// <summary>
    /// A point near the set boundary — moderate iteration count.
    /// </summary>
    [Benchmark(Description = "Boundary (-0.75, 0.1)")]
    public int ComputeBoundary()
    {
        return FractalCalculator.ComputeIterations(-0.75, 0.1, MaxIterations);
    }
}

