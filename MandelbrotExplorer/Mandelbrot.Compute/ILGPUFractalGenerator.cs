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
    private readonly Action<Index1D, ArrayView1D<byte, Stride1D.Dense>, int, int, int, double, double, double, double> _kernel;

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
            Index1D, ArrayView1D<byte, Stride1D.Dense>, int, int, int, double, double, double, double>(MandelbrotKernel);
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
            output[offset] = 0;     // B
            output[offset + 1] = 0; // G
            output[offset + 2] = 0; // R
            output[offset + 3] = 255; // A
        }
        else
        {
            double t = (double)iterations / maxIterations;
            byte r = (byte)(9 * (1 - t) * t * t * t * 255);
            byte g = (byte)(15 * (1 - t) * (1 - t) * t * t * 255);
            byte b = (byte)(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);

            output[offset] = b;
            output[offset + 1] = g;
            output[offset + 2] = r;
            output[offset + 3] = 255;
        }
    }

    public Task<byte[]> GenerateAsync(Viewport viewport, int maxIterations, CancellationToken ct)
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
