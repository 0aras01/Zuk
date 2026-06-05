using System.Threading;
using System.Threading.Tasks;
using Mandelbrot.Core.Models;

namespace Mandelbrot.Core.Services;

public class ParallelFractalGenerator : IFractalGenerator
{
    public string Name => "CPU (Parallel)";

    public bool IsGpuAccelerated => false;

    public Task<byte[]> GenerateAsync(Viewport viewport, int maxIterations, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            int width = viewport.ImageWidth;
            int height = viewport.ImageHeight;
            byte[] pixels = new byte[width * height * 4];

            ParallelOptions options = new ParallelOptions { CancellationToken = ct };

            Parallel.For(0, height, options, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    if (ct.IsCancellationRequested) return;

                    var (real, imag) = CoordinateMapper.PixelToComplex(x, y, viewport);
                    int iterations = MandelbrotCalculator.ComputeIterations(real, imag, maxIterations);

                    // Map iterations to a color (BGRA)
                    // Simple grayscale/hue for now, can be extracted to an IPalette service later.
                    byte r, g, b;
                    if (iterations == maxIterations)
                    {
                        r = 0; g = 0; b = 0; // Inside set = Black
                    }
                    else
                    {
                        // A simple continuous coloring or repeating palette
                        double t = (double)iterations / maxIterations;
                        r = (byte)(9 * (1 - t) * t * t * t * 255);
                        g = (byte)(15 * (1 - t) * (1 - t) * t * t * 255);
                        b = (byte)(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);
                    }

                    int offset = (y * width + x) * 4;
                    pixels[offset] = b;     // B
                    pixels[offset + 1] = g; // G
                    pixels[offset + 2] = r; // R
                    pixels[offset + 3] = 255; // A
                }
            });

            return pixels;
        }, ct);
    }
}
