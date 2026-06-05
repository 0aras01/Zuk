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
            double nu = iterations + 1.0 - Math.Log(logZn / 0.6931471805599453) / 0.6931471805599453;
            return Math.Max(0.0, nu);
        }

        return maxIterations;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void GetColor(
        double t,
        int paletteId,
        out byte r,
        out byte g,
        out byte b)
    {
        double ar = 0.5, ag = 0.5, ab = 0.5;
        double br = 0.5, bg = 0.5, bb = 0.5;
        double cr = 1.0, cg = 1.0, cb = 1.0;
        double dr = 0.0, dg = 0.10, db = 0.20;

        if (paletteId == 2) // Ice
        {
            ar = 0.5; ag = 0.5; ab = 0.5;
            br = 0.5; bg = 0.5; bb = 0.5;
            cr = 2.0; cg = 1.0; cb = 0.0;
            dr = 0.5; dg = 0.20; db = 0.25;
        }
        else if (paletteId == 3) // Rainbow
        {
            ar = 0.5; ag = 0.5; ab = 0.5;
            br = 0.5; bg = 0.5; bb = 0.5;
            cr = 1.0; cg = 1.0; cb = 1.0;
            dr = 0.0; dg = 0.33; db = 0.67;
        }
        else if (paletteId == 4) // Forest
        {
            ar = 0.8; ag = 0.5; ab = 0.4;
            br = 0.2; bg = 0.4; bb = 0.2;
            cr = 2.0; cg = 1.0; cb = 1.0;
            dr = 0.0; dg = 0.25; db = 0.25;
        }

        // cos(2 * pi * (c * t + d))
        const double twoPi = 2.0 * 3.141592653589793;
        
        double valR = ar + br * Math.Cos(twoPi * (cr * t + dr));
        double valG = ag + bg * Math.Cos(twoPi * (cg * t + dg));
        double valB = ab + bb * Math.Cos(twoPi * (cb * t + db));

        r = (byte)(valR < 0.0 ? 0.0 : (valR > 1.0 ? 255.0 : valR * 255.0));
        g = (byte)(valG < 0.0 ? 0.0 : (valG > 1.0 ? 255.0 : valG * 255.0));
        b = (byte)(valB < 0.0 ? 0.0 : (valB > 1.0 ? 255.0 : valB * 255.0));
    }
}

