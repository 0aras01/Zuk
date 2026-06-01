using System.Threading;
using System.Threading.Tasks;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Enums;
using System;

namespace Mandelbrot.Core.Services;

public class ParallelFractalGenerator : IFractalGenerator
{
    public Task<byte[]> GenerateAsync(Viewport viewport, int maxIterations, ColorTheme theme, CancellationToken ct)
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

                    byte r = 0, g = 0, b = 0;
                    if (iterations != maxIterations)
                    {
                        double t = (double)iterations / maxIterations;

                        switch (theme)
                        {
                            case ColorTheme.Fire:
                                r = (byte)(t * 255);
                                g = (byte)(t * t * 255);
                                b = (byte)(t * t * t * 255);
                                break;
                            case ColorTheme.Neon:
                                r = (byte)(Math.Sin(t * Math.PI) * 255);
                                g = (byte)(Math.Sin(t * Math.PI * 2) * 255);
                                b = (byte)(Math.Sin(t * Math.PI * 4) * 255);
                                break;
                            case ColorTheme.Gold:
                                r = (byte)(t * 255);
                                g = (byte)(t * 200);
                                b = (byte)(t * 100);
                                break;
                            case ColorTheme.Classic:
                            default:
                                r = (byte)(9 * (1 - t) * t * t * t * 255);
                                g = (byte)(15 * (1 - t) * (1 - t) * t * t * 255);
                                b = (byte)(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);
                                break;
                        }
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
