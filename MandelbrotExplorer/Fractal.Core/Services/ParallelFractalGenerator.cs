using System.Threading;
using System.Threading.Tasks;
using Fractal.Core.Models;

namespace Fractal.Core.Services;

public class ParallelFractalGenerator : IFractalGenerator
{
    public string Name => "CPU (Parallel)";

    public bool IsGpuAccelerated => false;

    public Task<byte[]> GenerateAsync(Viewport viewport, int maxIterations, int paletteId, FractalSettings settings, CancellationToken ct)
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
                    double smoothIter = FractalCalculator.ComputeSmoothIterations(real, imag, maxIterations, settings);

                    byte r, g, b;
                    if (smoothIter >= maxIterations)
                    {
                        r = 0; g = 0; b = 0; // Inside set = Black
                    }
                    else
                    {
                        // Map smooth iterations value to [0.0, 1.0] and get cosine color
                        double t = smoothIter / maxIterations;
                        FractalCalculator.GetColor(t, paletteId, out r, out g, out b);
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

