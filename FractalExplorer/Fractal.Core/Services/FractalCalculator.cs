using Fractal.Core.Models;

namespace Fractal.Core.Services;

/// <summary>
/// Provides mathematical calculations for the Mandelbrot set.
/// </summary>
public static class FractalCalculator
{
    /// <summary>
    /// Computes the number of iterations for a given point on the complex plane before it escapes.
    /// </summary>
    public static int ComputeIterations(double real, double imag, int maxIterations, FractalSettings settings = default)
    {
        double zReal;
        double zImag;
        double cReal;
        double cImag;

        if (settings.Type == FractalType.Julia)
        {
            zReal = real;
            zImag = imag;
            cReal = (double)settings.JuliaCReal;
            cImag = (double)settings.JuliaCImag;
        }
        else
        {
            zReal = 0;
            zImag = 0;
            cReal = real;
            cImag = imag;
        }

        int iterations = 0;

        if (settings.Type == FractalType.BurningShip)
        {
            while (zReal * zReal + zImag * zImag <= 4 && iterations < maxIterations)
            {
                double tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = Math.Abs(zReal * zImag) * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Tricorn)
        {
            while (zReal * zReal + zImag * zImag <= 4 && iterations < maxIterations)
            {
                double tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = -2.0 * zReal * zImag + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Celtic)
        {
            while (zReal * zReal + zImag * zImag <= 4 && iterations < maxIterations)
            {
                double tempReal = Math.Abs(zReal * zReal - zImag * zImag) + cReal;
                zImag = 2.0 * zReal * zImag + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Buffalo)
        {
            while (zReal * zReal + zImag * zImag <= 4 && iterations < maxIterations)
            {
                double tempReal = Math.Abs(zReal * zReal - zImag * zImag) + cReal;
                zImag = Math.Abs(zReal * zImag) * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Multibrot3)
        {
            while (zReal * zReal + zImag * zImag <= 4 && iterations < maxIterations)
            {
                double tempReal = zReal * (zReal * zReal - 3.0 * zImag * zImag) + cReal;
                zImag = zImag * (3.0 * zReal * zReal - zImag * zImag) + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else
        {
            while (zReal * zReal + zImag * zImag <= 4 && iterations < maxIterations)
            {
                double tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = 2 * zReal * zImag + cImag;
                zReal = tempReal;
                iterations++;
            }
        }

        return iterations;
    }

    public static int ComputeIterations(DoubleDouble real, DoubleDouble imag, int maxIterations, FractalSettings settings = default)
    {
        DoubleDouble zReal;
        DoubleDouble zImag;
        DoubleDouble cReal;
        DoubleDouble cImag;

        if (settings.Type == FractalType.Julia)
        {
            zReal = real;
            zImag = imag;
            cReal = settings.JuliaCReal;
            cImag = settings.JuliaCImag;
        }
        else
        {
            zReal = 0.0;
            zImag = 0.0;
            cReal = real;
            cImag = imag;
        }

        int iterations = 0;

        if (settings.Type == FractalType.BurningShip)
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = (zReal * zImag).Abs() * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Tricorn)
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = zReal * zImag * -2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Celtic)
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
                zImag = zReal * zImag * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Buffalo)
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
                zImag = (zReal * zImag).Abs() * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Multibrot3)
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

        return iterations;
    }

    public static double ComputeSmoothIterations(DoubleDouble real, DoubleDouble imag, int maxIterations, FractalSettings settings = default)
    {
        DoubleDouble zReal;
        DoubleDouble zImag;
        DoubleDouble cReal;
        DoubleDouble cImag;

        if (settings.Type == FractalType.Julia)
        {
            zReal = real;
            zImag = imag;
            cReal = settings.JuliaCReal;
            cImag = settings.JuliaCImag;
        }
        else
        {
            zReal = 0.0;
            zImag = 0.0;
            cReal = real;
            cImag = imag;
        }

        int iterations = 0;

        if (settings.Type == FractalType.BurningShip)
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = (zReal * zImag).Abs() * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Tricorn)
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = zReal * zReal - zImag * zImag + cReal;
                zImag = zReal * zImag * -2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Celtic)
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
                zImag = zReal * zImag * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Buffalo)
        {
            while (zReal * zReal + zImag * zImag < 4.0 && iterations < maxIterations)
            {
                DoubleDouble tempReal = (zReal * zReal - zImag * zImag).Abs() + cReal;
                zImag = (zReal * zImag).Abs() * 2.0 + cImag;
                zReal = tempReal;
                iterations++;
            }
        }
        else if (settings.Type == FractalType.Multibrot3)
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

        if (iterations < maxIterations)
        {
            double logZn = Math.Log((double)(zReal * zReal + zImag * zImag)) * 0.5;
            double logDegree = settings.Type == FractalType.Multibrot3 ? 1.0986122886681096 : 0.6931471805599453;
            double nu = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
            return Math.Max(0.0, nu);
        }

        return maxIterations;
    }
}
