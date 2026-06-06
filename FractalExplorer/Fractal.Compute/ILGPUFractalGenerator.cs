using System;
using System.Threading;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;
using Fractal.Core.Models;
using Fractal.Core.Services;

namespace Fractal.Compute;

public class ILGPUFractalGenerator : IFractalGenerator, IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, int, int, int, int, DoubleDouble, DoubleDouble, DoubleDouble, DoubleDouble, DoubleDouble, DoubleDouble> _kernel;

    public string Name => $"GPU (ILGPU - {_accelerator.Name})";

    public bool IsGpuAccelerated => true;

    public ILGPUFractalGenerator()
    {
        _context = Context.CreateDefault();
        _accelerator = _context.GetPreferredDevice(preferCPU: false).CreateAccelerator(_context);

        _kernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, int, int, int, int, DoubleDouble, DoubleDouble, DoubleDouble, DoubleDouble, DoubleDouble, DoubleDouble>(FractalKernel);
    }

    public static void FractalKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> output,
        int width,
        int height,
        int maxIterations,
        int fractalType,
        DoubleDouble juliaCReal,
        DoubleDouble juliaCImag,
        DoubleDouble realMin,
        DoubleDouble realMax,
        DoubleDouble imagMin,
        DoubleDouble imagMax)
    {
        int x = index % width;
        int y = index / width;

        DoubleDouble realRange = realMax - realMin;
        DoubleDouble imagRange = imagMax - imagMin;

        DoubleDouble real = realMin + (realRange * (double)x / width);
        DoubleDouble imag = imagMax - (imagRange * (double)y / height);

        DoubleDouble zReal;
        DoubleDouble zImag;
        DoubleDouble cReal;
        DoubleDouble cImag;

        if (fractalType == 1) // Julia
        {
            zReal = real;
            zImag = imag;
            cReal = juliaCReal;
            cImag = juliaCImag;
        }
        else
        {
            zReal = 0.0;
            zImag = 0.0;
            cReal = real;
            cImag = imag;
        }

        int iterations = 0;

        if (fractalType == 2) // Burning Ship
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = (zReal * zImag).Abs() * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (fractalType == 3) // Tricorn
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = zReal * zImag * -2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (fractalType == 4) // Celtic
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
                zImag = zReal * zImag * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (fractalType == 5) // Buffalo
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
                zImag = (zReal * zImag).Abs() * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (fractalType == 6) // Multibrot3
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = zReal * (zReal * zReal - zImag * zImag * 3.0) + cReal;
                zImag = zImag * (zReal * zReal * 3.0 - zImag * zImag) + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = zReal * zImag * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }

        double smoothIter = maxIterations;
        if (iterations < maxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            double logDegree = fractalType == 6 ? 1.0986122886681096 : 0.6931471805599453;
            smoothIter = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
            if (smoothIter < 0.0) smoothIter = 0.0;
        }

        output[index] = smoothIter;
    }

    public Task<(byte[] Pixels, double[] Iterations)> GenerateAsync(Viewport viewport, int maxIterations, GradientPalette palette, double paletteOffset, FractalSettings settings, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            int totalPixels = viewport.ImageWidth * viewport.ImageHeight;
            using var buffer = _accelerator.Allocate1D<double>(totalPixels);

            ct.ThrowIfCancellationRequested();

            _kernel(
                totalPixels,
                buffer.View,
                viewport.ImageWidth,
                viewport.ImageHeight,
                maxIterations,
                (int)settings.Type,
                settings.JuliaCReal,
                settings.JuliaCImag,
                viewport.Plane.RealMin,
                viewport.Plane.RealMax,
                viewport.Plane.ImagMin,
                viewport.Plane.ImagMax);

            _accelerator.Synchronize();
            ct.ThrowIfCancellationRequested();

            double[] iterations = buffer.GetAsArray1D();
            byte[] pixels = new byte[totalPixels * 4];

            ParallelOptions options = new ParallelOptions { CancellationToken = ct };
            Parallel.For(0, totalPixels, options, i =>
            {
                double smoothIter = iterations[i];
                byte r, g, b;
                
                if (smoothIter >= maxIterations)
                {
                    r = 0; g = 0; b = 0;
                }
                else
                {
                    double t = smoothIter / maxIterations;
                    palette.GetColor(t, paletteOffset, out r, out g, out b);
                }

                int offset = i * 4;
                pixels[offset] = b;
                pixels[offset + 1] = g;
                pixels[offset + 2] = r;
                pixels[offset + 3] = 255;
            });

            return (pixels, iterations);
        }, ct);
    }

    public void Dispose()
    {
        _accelerator?.Dispose();
        _context?.Dispose();
    }
}

