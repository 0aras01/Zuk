using System.Threading;
using System.Threading.Tasks;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Enums;

namespace Mandelbrot.Core.Services;

/// <summary>
/// Interface for generating the fractal image data.
/// </summary>
public interface IFractalGenerator
{
    /// <summary>
    /// Generates a byte array of pixel data (BGRA32 format) for the given viewport.
    /// </summary>
    Task<byte[]> GenerateAsync(Viewport viewport, int maxIterations, ColorTheme theme, CancellationToken ct);
}
