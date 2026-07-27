import re

with open('FractalExplorer/Fractal.Compute/ILGPUFractalGenerator.cs', 'w', encoding='utf-8') as f:
    f.write("""using System;
using System.Threading;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Fractal.Core.Exceptions;

namespace Fractal.Compute;

public class ILGPUFractalGenerator : IFractalGenerator, IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private bool _disposed = false;
    private const int LutSize = 4096;

    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _mandelbrotKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _juliaKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _burningShipKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _tricornKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _celticKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _buffaloKernel;
    private readonly Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> _multibrot3Kernel;

    public string Name => $"GPU (ILGPU - {_accelerator.Name})";

    public bool IsGpuAccelerated => true;

    public ILGPUFractalGenerator()
    {
        _context = Context.CreateDefault();
        try
        {
            _accelerator = _context.GetPreferredDevice(preferCPU: false).CreateAccelerator(_context);
        }
        catch (Exception ex)
        {
            _context.Dispose();
            throw new GpuAccelerationNotAvailableException("No suitable GPU accelerator was found.", ex);
        }

        _mandelbrotKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(MandelbrotKernel);
        _juliaKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(JuliaKernel);
        _burningShipKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(BurningShipKernel);
        _tricornKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(TricornKernel);
        _celticKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(CelticKernel);
        _buffaloKernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(BuffaloKernel);
        _multibrot3Kernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams>(Multibrot3Kernel);
    }

    private static void MapCoordinates(Index1D index, FractalParams p, out DoubleDouble real, out DoubleDouble imag)
    {
        int x = index % p.Width;
        int y = index / p.Width;

        DoubleDouble realRange = p.RealMax - p.RealMin;
        DoubleDouble imagRange = p.ImagMax - p.ImagMin;

        DoubleDouble dx = new DoubleDouble(x, 0.0);
        DoubleDouble dw = new DoubleDouble(p.Width, 0.0);
        real = p.RealMin + (realRange * (dx / dw));

        DoubleDouble dy = new DoubleDouble(y, 0.0);
        DoubleDouble dh = new DoubleDouble(p.Height, 0.0);
        imag = p.ImagMax - (imagRange * (dy / dh));
    }

    private static void WriteOutput(Index1D index, double smoothIter, FractalParams p, ArrayView1D<double, Stride1D.Dense> outputIterations, ArrayView1D<byte, Stride1D.Dense> outputPixels, ArrayView1D<byte, Stride1D.Dense> lut)
    {
        outputIterations[index] = smoothIter;
        int offset = index * 4;

        if (smoothIter >= p.MaxIterations)
        {
            outputPixels[offset] = 0;
            outputPixels[offset + 1] = 0;
            outputPixels[offset + 2] = 0;
            outputPixels[offset + 3] = 255;
        }
        else
        {
            double t = smoothIter / p.MaxIterations;
            int lutIndex = (int)(t * (4096 - 1)) * 4;
            outputPixels[offset] = lut[lutIndex];
            outputPixels[offset + 1] = lut[lutIndex + 1];
            outputPixels[offset + 2] = lut[lutIndex + 2];
            outputPixels[offset + 3] = 255;
        }
    }

    public static void MandelbrotKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> outputIterations, ArrayView1D<byte, Stride1D.Dense> outputPixels, ArrayView1D<byte, Stride1D.Dense> lut, FractalParams p)
    {
        MapCoordinates(index, p, out DoubleDouble cReal, out DoubleDouble cImag);
        DoubleDouble zReal = DoubleDouble.Zero;
        DoubleDouble zImag = DoubleDouble.Zero;

        int iterations = 0;
        while (zReal * zReal + zImag * zImag < DoubleDouble.Four && iterations < p.MaxIterations)
        {
            DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
            zImag = zReal * zImag * DoubleDouble.Two + cImag;
            zReal = tempReal;
            iterations++;
        }

        double smoothIter = p.MaxIterations;
        if (iterations < p.MaxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            double logDegree = 0.6931471805599453;
            smoothIter = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
            if (smoothIter < 0.0) smoothIter = 0.0;
        }

        WriteOutput(index, smoothIter, p, outputIterations, outputPixels, lut);
    }

    public static void JuliaKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> outputIterations, ArrayView1D<byte, Stride1D.Dense> outputPixels, ArrayView1D<byte, Stride1D.Dense> lut, FractalParams p)
    {
        MapCoordinates(index, p, out DoubleDouble zReal, out DoubleDouble zImag);
        DoubleDouble cReal = p.JuliaCReal;
        DoubleDouble cImag = p.JuliaCImag;

        int iterations = 0;
        while (zReal * zReal + zImag * zImag < DoubleDouble.Four && iterations < p.MaxIterations)
        {
            DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
            zImag = zReal * zImag * DoubleDouble.Two + cImag;
            zReal = tempReal;
            iterations++;
        }

        double smoothIter = p.MaxIterations;
        if (iterations < p.MaxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            double logDegree = 0.6931471805599453;
            smoothIter = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
            if (smoothIter < 0.0) smoothIter = 0.0;
        }

        WriteOutput(index, smoothIter, p, outputIterations, outputPixels, lut);
    }

    public static void BurningShipKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> outputIterations, ArrayView1D<byte, Stride1D.Dense> outputPixels, ArrayView1D<byte, Stride1D.Dense> lut, FractalParams p)
    {
        MapCoordinates(index, p, out DoubleDouble cReal, out DoubleDouble cImag);
        DoubleDouble zReal = DoubleDouble.Zero;
        DoubleDouble zImag = DoubleDouble.Zero;

        int iterations = 0;
        while (zReal * zReal + zImag * zImag < DoubleDouble.Four && iterations < p.MaxIterations)
        {
            DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
            zImag = (zReal * zImag).Abs() * DoubleDouble.Two + cImag;
            zReal = tempReal;
            iterations++;
        }

        double smoothIter = p.MaxIterations;
        if (iterations < p.MaxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            double logDegree = 0.6931471805599453;
            smoothIter = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
            if (smoothIter < 0.0) smoothIter = 0.0;
        }

        WriteOutput(index, smoothIter, p, outputIterations, outputPixels, lut);
    }

    public static void TricornKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> outputIterations, ArrayView1D<byte, Stride1D.Dense> outputPixels, ArrayView1D<byte, Stride1D.Dense> lut, FractalParams p)
    {
        MapCoordinates(index, p, out DoubleDouble cReal, out DoubleDouble cImag);
        DoubleDouble zReal = DoubleDouble.Zero;
        DoubleDouble zImag = DoubleDouble.Zero;

        int iterations = 0;
        while (zReal * zReal + zImag * zImag < DoubleDouble.Four && iterations < p.MaxIterations)
        {
            DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
            zImag = zReal * zImag * new DoubleDouble(-2.0, 0.0) + cImag;
            zReal = tempReal;
            iterations++;
        }

        double smoothIter = p.MaxIterations;
        if (iterations < p.MaxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            double logDegree = 0.6931471805599453;
            smoothIter = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
            if (smoothIter < 0.0) smoothIter = 0.0;
        }

        WriteOutput(index, smoothIter, p, outputIterations, outputPixels, lut);
    }

    public static void CelticKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> outputIterations, ArrayView1D<byte, Stride1D.Dense> outputPixels, ArrayView1D<byte, Stride1D.Dense> lut, FractalParams p)
    {
        MapCoordinates(index, p, out DoubleDouble cReal, out DoubleDouble cImag);
        DoubleDouble zReal = DoubleDouble.Zero;
        DoubleDouble zImag = DoubleDouble.Zero;

        int iterations = 0;
        while (zReal * zReal + zImag * zImag < DoubleDouble.Four && iterations < p.MaxIterations)
        {
            DoubleDouble tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
            zImag = zReal * zImag * DoubleDouble.Two + cImag;
            zReal = tempReal;
            iterations++;
        }

        double smoothIter = p.MaxIterations;
        if (iterations < p.MaxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            double logDegree = 0.6931471805599453;
            smoothIter = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
            if (smoothIter < 0.0) smoothIter = 0.0;
        }

        WriteOutput(index, smoothIter, p, outputIterations, outputPixels, lut);
    }

    public static void BuffaloKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> outputIterations, ArrayView1D<byte, Stride1D.Dense> outputPixels, ArrayView1D<byte, Stride1D.Dense> lut, FractalParams p)
    {
        MapCoordinates(index, p, out DoubleDouble cReal, out DoubleDouble cImag);
        DoubleDouble zReal = DoubleDouble.Zero;
        DoubleDouble zImag = DoubleDouble.Zero;

        int iterations = 0;
        while (zReal * zReal + zImag * zImag < DoubleDouble.Four && iterations < p.MaxIterations)
        {
            DoubleDouble tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
            zImag = (zReal * zImag).Abs() * DoubleDouble.Two + cImag;
            zReal = tempReal;
            iterations++;
        }

        double smoothIter = p.MaxIterations;
        if (iterations < p.MaxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            double logDegree = 0.6931471805599453;
            smoothIter = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
            if (smoothIter < 0.0) smoothIter = 0.0;
        }

        WriteOutput(index, smoothIter, p, outputIterations, outputPixels, lut);
    }

    public static void Multibrot3Kernel(Index1D index, ArrayView1D<double, Stride1D.Dense> outputIterations, ArrayView1D<byte, Stride1D.Dense> outputPixels, ArrayView1D<byte, Stride1D.Dense> lut, FractalParams p)
    {
        MapCoordinates(index, p, out DoubleDouble cReal, out DoubleDouble cImag);
        DoubleDouble zReal = DoubleDouble.Zero;
        DoubleDouble zImag = DoubleDouble.Zero;
        DoubleDouble three = new DoubleDouble(3.0, 0.0);

        int iterations = 0;
        while (zReal * zReal + zImag * zImag < DoubleDouble.Four && iterations < p.MaxIterations)
        {
            DoubleDouble tempReal = zReal * (zReal * zReal - zImag * zImag * three) + cReal;
            zImag = zImag * (zReal * zReal * three - zImag * zImag) + cImag;
            zReal = tempReal;
            iterations++;
        }

        double smoothIter = p.MaxIterations;
        if (iterations < p.MaxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            double logDegree = 1.0986122886681096;
            smoothIter = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
            if (smoothIter < 0.0) smoothIter = 0.0;
        }

        WriteOutput(index, smoothIter, p, outputIterations, outputPixels, lut);
    }

    public Task<(byte[] Pixels, double[] Iterations)> GenerateAsync(Viewport viewport, int maxIterations, GradientPalette palette, double paletteOffset, FractalSettings settings, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            int totalPixels = viewport.ImageWidth * viewport.ImageHeight;

            using var iterationsBuffer = _accelerator.Allocate1D<double>(totalPixels);
            using var pixelsBuffer = _accelerator.Allocate1D<byte>(totalPixels * 4);

            // Generowanie tablicy LUT (Look-Up Table) na CPU i kopiowanie na GPU
            byte[] lutData = new byte[LutSize * 4];
            for (int i = 0; i < LutSize; i++)
            {
                double t = (double)i / (LutSize - 1);
                palette.GetColor(t, paletteOffset, out byte r, out byte g, out byte b);
                lutData[i * 4] = b;
                lutData[i * 4 + 1] = g;
                lutData[i * 4 + 2] = r;
                lutData[i * 4 + 3] = 255;
            }
            using var lutBuffer = _accelerator.Allocate1D<byte>(lutData);

            FractalParams parameters = new FractalParams
            {
                Width = viewport.ImageWidth,
                Height = viewport.ImageHeight,
                MaxIterations = maxIterations,
                RealMin = viewport.Plane.RealMin,
                RealMax = viewport.Plane.RealMax,
                ImagMin = viewport.Plane.ImagMin,
                ImagMax = viewport.Plane.ImagMax,
                JuliaCReal = settings.JuliaCReal,
                JuliaCImag = settings.JuliaCImag
            };

            // Wybór odpowiedniego kernela bez warp divergence!
            Action<Index1D, ArrayView1D<double, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, ArrayView1D<byte, Stride1D.Dense>, FractalParams> selectedKernel = settings.Type switch
            {
                FractalType.Julia => _juliaKernel,
                FractalType.BurningShip => _burningShipKernel,
                FractalType.Tricorn => _tricornKernel,
                FractalType.Celtic => _celticKernel,
                FractalType.Buffalo => _buffaloKernel,
                FractalType.Multibrot3 => _multibrot3Kernel,
                _ => _mandelbrotKernel
            };

            selectedKernel(
                totalPixels,
                iterationsBuffer.View,
                pixelsBuffer.View,
                lutBuffer.View,
                parameters);

            ct.ThrowIfCancellationRequested();

            // Kopiowanie wyników z powrotem do CPU (GetAsArray1D domyślnie czeka na strumień)
            double[] iterations = iterationsBuffer.GetAsArray1D();
            byte[] pixels = pixelsBuffer.GetAsArray1D();

            return (pixels, iterations);
        }, ct);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _accelerator?.Dispose();
        _context?.Dispose();

        _disposed = true;
    }
}
""")
