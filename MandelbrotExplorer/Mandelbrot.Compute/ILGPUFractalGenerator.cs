using System;
using System.Threading;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;
using Mandelbrot.Core.Enums;

namespace Mandelbrot.Compute;

public class ILGPUFractalGenerator : IFractalGenerator, IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly Action<Index1D, ArrayView1D<byte, Stride1D.Dense>, int, int, int, int, double, double, double, double> _kernel;

    public ILGPUFractalGenerator()
    {
        _context = Context.CreateDefault();
        _accelerator = _context.GetPreferredDevice(preferCPU: false).CreateAccelerator(_context);

        _kernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<byte, Stride1D.Dense>, int, int, int, int, double, double, double, double>(MandelbrotKernel);
    }

    public static void MandelbrotKernel(
        Index1D index,
        ArrayView1D<byte, Stride1D.Dense> output,
        int width,
        int height,
        int maxIterations,
        int themeIndex,
        double realMin,
        double realMax,
        double imagMin,
        double imagMax)
    {
        int x = index % width;
        int y = index / width;

        double realRange = realMax - realMin;
        double imagRange = imagMax - imagMin;

        double real = realMin + (x * realRange / width);
        double imag = imagMax - (y * imagRange / height);

        double zReal = 0;
        double zImag = 0;
        int iterations = 0;

        while (zReal * zReal + zImag * zImag <= 4 && iterations < maxIterations)
        {
            double tempReal = zReal * zReal - zImag * zImag + real;
            zImag = 2 * zReal * zImag + imag;
            zReal = tempReal;
            iterations++;
        }

        int offset = index * 4;

        if (iterations == maxIterations)
        {
            output[offset] = 0;
            output[offset + 1] = 0;
            output[offset + 2] = 0;
            output[offset + 3] = 255;
        }
        else
        {
            double t = (double)iterations / maxIterations;
            byte r = 0, g = 0, b = 0;

            if (themeIndex == 1) // Fire
            {
                r = (byte)(t * 255);
                g = (byte)(t * t * 255);
                b = (byte)(t * t * t * 255);
            }
            else if (themeIndex == 2) // Neon
            {
                r = (byte)(ILGPU.Algorithms.XMath.Sin(t * 3.14159) * 255);
                g = (byte)(ILGPU.Algorithms.XMath.Sin(t * 3.14159 * 2) * 255);
                b = (byte)(ILGPU.Algorithms.XMath.Sin(t * 3.14159 * 4) * 255);
            }
            else if (themeIndex == 3) // Gold
            {
                r = (byte)(t * 255);
                g = (byte)(t * 200);
                b = (byte)(t * 100);
            }
            else // Classic
            {
                r = (byte)(9 * (1 - t) * t * t * t * 255);
                g = (byte)(15 * (1 - t) * (1 - t) * t * t * 255);
                b = (byte)(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);
            }

            output[offset] = b;
            output[offset + 1] = g;
            output[offset + 2] = r;
            output[offset + 3] = 255;
        }
    }

    public Task<byte[]> GenerateAsync(Viewport viewport, int maxIterations, ColorTheme theme, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            int totalPixels = viewport.ImageWidth * viewport.ImageHeight;

            using var buffer = _accelerator.Allocate1D<byte>(totalPixels * 4);

            _kernel(
                totalPixels,
                buffer.View,
                viewport.ImageWidth,
                viewport.ImageHeight,
                maxIterations,
                (int)theme,
                viewport.Plane.RealMin,
                viewport.Plane.RealMax,
                viewport.Plane.ImagMin,
                viewport.Plane.ImagMax);

            _accelerator.Synchronize();

            ct.ThrowIfCancellationRequested();

            return buffer.GetAsArray1D();
        }, ct);
    }

    public void Dispose()
    {
        _accelerator?.Dispose();
        _context?.Dispose();
    }
}
