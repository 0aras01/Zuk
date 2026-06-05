using System;
using System.Threading;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;

namespace Mandelbrot.Compute;

public class ILGPUFractalGenerator : IFractalGenerator, IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly Action<Index1D, ArrayView1D<byte, Stride1D.Dense>, int, int, int, int, DoubleDouble, DoubleDouble, DoubleDouble, DoubleDouble> _kernel;

    public string Name => $"GPU (ILGPU - {_accelerator.Name})";

    public bool IsGpuAccelerated => true;

    public ILGPUFractalGenerator()
    {
        // Initialize ILGPU Context
        // Use default configuration which prefers GPU (OpenCL/CUDA) over CPU
        _context = Context.CreateDefault();

        // Find best accelerator
        _accelerator = _context.GetPreferredDevice(preferCPU: false).CreateAccelerator(_context);

        // Compile the kernel for the selected accelerator
        _kernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<byte, Stride1D.Dense>, int, int, int, int, DoubleDouble, DoubleDouble, DoubleDouble, DoubleDouble>(MandelbrotKernel);
    }

    /// <summary>
    /// The GPU kernel that calculates the Mandelbrot set.
    /// This method is executed on the device (GPU).
    /// </summary>
    public static void MandelbrotKernel(
        Index1D index,
        ArrayView1D<byte, Stride1D.Dense> output,
        int width,
        int height,
        int maxIterations,
        int paletteId,
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

        DoubleDouble zReal = 0.0;
        DoubleDouble zImag = 0.0;
        int iterations = 0;

        while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
        {
            DoubleDouble tempReal = zReal * zReal - zImag * zImag + real;
            zImag = zReal * zImag * 2.0 + imag;
            zReal = tempReal;
            iterations++;
        }

        double smoothIter = maxIterations;
        if (iterations < maxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            smoothIter = iterations + 1.0 - Math.Log(logZn / 0.6931471805599453) / 0.6931471805599453;
            if (smoothIter < 0.0) smoothIter = 0.0;
        }

        int offset = index * 4;

        byte r, g, b;
        if (smoothIter >= maxIterations)
        {
            r = 0; g = 0; b = 0;
        }
        else
        {
            double t = smoothIter / maxIterations;
            MandelbrotCalculator.GetColor(t, paletteId, out r, out g, out b);
        }

        output[offset] = b;
        output[offset + 1] = g;
        output[offset + 2] = r;
        output[offset + 3] = 255;
    }

    public Task<byte[]> GenerateAsync(Viewport viewport, int maxIterations, int paletteId, CancellationToken ct)
    {
        // Run ILGPU pipeline asynchronously
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            int totalPixels = viewport.ImageWidth * viewport.ImageHeight;

            // Allocate memory on the GPU
            using var buffer = _accelerator.Allocate1D<byte>(totalPixels * 4);

            ct.ThrowIfCancellationRequested();

            // Execute the kernel
            _kernel(
                totalPixels,
                buffer.View,
                viewport.ImageWidth,
                viewport.ImageHeight,
                maxIterations,
                paletteId,
                viewport.Plane.RealMin,
                viewport.Plane.RealMax,
                viewport.Plane.ImagMin,
                viewport.Plane.ImagMax);

            // Wait for GPU to finish
            _accelerator.Synchronize();

            ct.ThrowIfCancellationRequested();

            // Copy data back from GPU to CPU
            return buffer.GetAsArray1D();
        }, ct);
    }

    public void Dispose()
    {
        _accelerator?.Dispose();
        _context?.Dispose();
    }
}
