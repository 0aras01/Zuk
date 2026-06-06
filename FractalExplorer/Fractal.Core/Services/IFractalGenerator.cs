using System.Threading;
using System.Threading.Tasks;
using Fractal.Core.Models;

namespace Fractal.Core.Services;

/// <summary>
/// Interface for generating the fractal image data.
/// </summary>
public interface IFractalGenerator
{
    /// <summary>
    /// A human-readable name for this generator (e.g. "GPU (ILGPU)" or "CPU (Parallel)").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Indicates whether this generator uses GPU acceleration.
    /// </summary>
    bool IsGpuAccelerated { get; }

    /// <summary>
    /// Generates a byte array of pixel data (BGRA32 format) for the given viewport, and the double array of iteration data.
    /// </summary>
    Task<(byte[] Pixels, double[] Iterations)> GenerateAsync(Viewport viewport, int maxIterations, GradientPalette palette, double paletteOffset, FractalSettings settings, CancellationToken ct);
}

