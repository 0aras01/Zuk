using Mandelbrot.Core.Models;

namespace Mandelbrot.Core.Services;

/// <summary>
/// Provides mathematical calculations for the Mandelbrot set.
/// </summary>
public static class MandelbrotCalculator
{
    /// <summary>
    /// Computes the number of iterations for a given point on the complex plane before it escapes.
    /// </summary>
    public static int ComputeIterations(double real, double imag, int maxIterations)
    {
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

        return iterations;
    }

    public static int ComputeIterations(DoubleDouble real, DoubleDouble imag, int maxIterations)
    {
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

        return iterations;
    }
}
