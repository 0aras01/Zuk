using System.Threading;
using System.Threading.Tasks;
using Fractal.Core.Models;

namespace Fractal.Core.Services;

public class ParallelFractalGenerator : IFractalGenerator
{
    public string Name => "CPU (Parallel)";

    public bool IsGpuAccelerated => false;

    public Task<(byte[] Pixels, double[] Iterations)> GenerateAsync(Viewport viewport, int maxIterations, GradientPalette palette, double paletteOffset, FractalSettings settings, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            int width = viewport.ImageWidth;
            int height = viewport.ImageHeight;
            byte[] pixels = new byte[width * height * 4];
            double[] iterations = new double[width * height];

            DoubleDouble centerReal = (viewport.Plane.RealMin + viewport.Plane.RealMax) * 0.5;
            DoubleDouble centerImag = (viewport.Plane.ImagMin + viewport.Plane.ImagMax) * 0.5;

            DoubleDouble[]? refRe = null;
            DoubleDouble[]? refIm = null;
            int refEscapeIter = maxIterations;
            bool usePerturbation = (settings.Type == FractalType.Mandelbrot || settings.Type == FractalType.Julia);

            if (usePerturbation)
            {
                (refRe, refIm, refEscapeIter) = PerturbationEngine.PrecalculateReferenceOrbit(centerReal, centerImag, maxIterations, settings);
            }

            ParallelOptions options = new ParallelOptions { CancellationToken = ct };

            Parallel.For(0, height, options, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    if (ct.IsCancellationRequested) return;

                    var (real, imag) = CoordinateMapper.PixelToComplex(x, y, viewport);
                    
                    double smoothIter;
                    if (usePerturbation && refRe != null && refIm != null)
                    {
                        smoothIter = PerturbationEngine.ComputeSmoothIterations(
                            real, imag, centerReal, centerImag, refRe, refIm, refEscapeIter, maxIterations, settings);
                    }
                    else
                    {
                        smoothIter = FractalCalculator.ComputeSmoothIterations(real, imag, maxIterations, settings);
                    }

                    int idx = y * width + x;
                    iterations[idx] = smoothIter;

                    byte r, g, b;
                    if (smoothIter >= maxIterations)
                    {
                        r = 0; g = 0; b = 0; // Inside set = Black
                    }
                    else
                    {
                        double t = smoothIter / maxIterations;
                        palette.GetColor(t, paletteOffset, out r, out g, out b);
                    }

                    int offset = idx * 4;
                    pixels[offset] = b;     // B
                    pixels[offset + 1] = g; // G
                    pixels[offset + 2] = r; // R
                    pixels[offset + 3] = 255; // A
                }
            });

            return (pixels, iterations);
        }, ct);
    }
}

