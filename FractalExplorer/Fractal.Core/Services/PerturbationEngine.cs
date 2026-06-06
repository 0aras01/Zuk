using System;
using Fractal.Core.Models;

namespace Fractal.Core.Services;

public static class PerturbationEngine
{
    /// <summary>
    /// Precalculates the reference orbit for the center of the viewport in DoubleDouble precision.
    /// </summary>
    public static (DoubleDouble[] re, DoubleDouble[] im, int escapeIter) PrecalculateReferenceOrbit(
        DoubleDouble cReal, DoubleDouble cImag, int maxIterations, FractalSettings settings)
    {
        var re = new DoubleDouble[maxIterations];
        var im = new DoubleDouble[maxIterations];

        DoubleDouble zReal;
        DoubleDouble zImag;
        DoubleDouble seedReal;
        DoubleDouble seedImag;

        if (settings.Type == FractalType.Julia)
        {
            zReal = cReal;
            zImag = cImag;
            seedReal = settings.JuliaCReal;
            seedImag = settings.JuliaCImag;
        }
        else
        {
            zReal = 0.0;
            zImag = 0.0;
            seedReal = cReal;
            seedImag = cImag;
        }

        int escapeIter = maxIterations;

        for (int i = 0; i < maxIterations; i++)
        {
            re[i] = zReal;
            im[i] = zImag;

            // w_{n+1} = w_n^2 + c0
            DoubleDouble nextReal = zReal * zReal - zImag * zImag + seedReal;
            DoubleDouble nextImag = zReal * zImag * 2.0 + seedImag;

            zReal = nextReal;
            zImag = nextImag;

            if (zReal * zReal + zImag * zImag > 4.0)
            {
                escapeIter = i + 1;
                break;
            }
        }

        return (re, im, escapeIter);
    }

    /// <summary>
    /// Computes iterations for a single pixel using perturbation theory.
    /// Falls back to standard double-double if needed.
    /// </summary>
    public static double ComputeSmoothIterations(
        DoubleDouble pixelReal, DoubleDouble pixelImag,
        DoubleDouble centerReal, DoubleDouble centerImag,
        DoubleDouble[] refRe, DoubleDouble[] refIm, int refEscapeIter,
        int maxIterations, FractalSettings settings)
    {
        // Calculate deltaC = pixelC - centerC in high-precision, then downcast to double
        DoubleDouble deltaCRealDD;
        DoubleDouble deltaCImagDD;

        if (settings.Type == FractalType.Julia)
        {
            // For Julia, deltaC is 0 because the constant C is the same for all pixels.
            // The starting Z is different: z0 = pixelC, centerStartingZ = centerC.
            deltaCRealDD = 0.0;
            deltaCImagDD = 0.0;
        }
        else
        {
            deltaCRealDD = pixelReal - centerReal;
            deltaCImagDD = pixelImag - centerImag;
        }

        double dx = (double)deltaCRealDD;
        double dy = (double)deltaCImagDD;

        // Initialize perturbation delta (double precision)
        double dRe;
        double dIm;

        if (settings.Type == FractalType.Julia)
        {
            // For Julia, initial delta is the difference in starting Z
            dRe = (double)(pixelReal - centerReal);
            dIm = (double)(pixelImag - centerImag);
        }
        else
        {
            dRe = 0.0;
            dIm = 0.0;
        }

        int iterations = 0;
        bool fallback = false;

        while (iterations < maxIterations)
        {
            // If we've reached the end of the precalculated reference orbit, we must fall back
            if (iterations >= refEscapeIter)
            {
                fallback = true;
                break;
            }

            double wRe = (double)refRe[iterations];
            double wIm = (double)refIm[iterations];

            double zRe = wRe + dRe;
            double zIm = wIm + dIm;

            // Escape check: |z|^2 > 4.0
            double normSq = zRe * zRe + zIm * zIm;
            if (normSq > 4.0)
            {
                // Smooth iteration calculation in double precision
                double logZn = Math.Log(normSq) * 0.5;
                double logDegree = 0.6931471805599453; // ln(2)
                double nu = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
                return Math.Max(0.0, nu);
            }

            // Glitch detection: if delta becomes too close to -w (meaning z is very close to 0)
            // or if delta size is extremely small but should be growing, or simple scale error.
            // A common robust test is if |z|^2 is extremely small and we are far in, we might lose precision.
            if (normSq < 1e-15)
            {
                fallback = true;
                break;
            }

            // delta_{n+1} = 2 * w_n * delta_n + delta_n^2 + deltaC
            double nextDRe = 2.0 * (wRe * dRe - wIm * dIm) + (dRe * dRe - dIm * dIm) + dx;
            double nextDIm = 2.0 * (wRe * dIm + wIm * dRe) + (2.0 * dRe * dIm) + dy;

            dRe = nextDRe;
            dIm = nextDIm;
            iterations++;
        }

        if (fallback)
        {
            // Fallback: resume calculation using standard DoubleDouble starting from the current iteration's Z value
            DoubleDouble zRealDD = refRe[iterations] + dRe;
            DoubleDouble zImagDD = refIm[iterations] + dIm;

            DoubleDouble cRealDD = settings.Type == FractalType.Julia ? settings.JuliaCReal : pixelReal;
            DoubleDouble cImagDD = settings.Type == FractalType.Julia ? settings.JuliaCImag : pixelImag;

            while (iterations < maxIterations)
            {
                DoubleDouble zRealSq = zRealDD * zRealDD;
                DoubleDouble zImagSq = zImagDD * zImagDD;

                if (zRealSq + zImagSq > 4.0)
                {
                    double norm = (double)(zRealSq + zImagSq);
                    double logZn = Math.Log(norm) * 0.5;
                    double logDegree = 0.6931471805599453;
                    double nu = iterations + 1.0 - Math.Log(logZn / logDegree) / logDegree;
                    return Math.Max(0.0, nu);
                }

                DoubleDouble nextReal = zRealSq - zImagSq + cRealDD;
                DoubleDouble nextImag = zRealDD * zImagDD * 2.0 + cImagDD;

                zRealDD = nextReal;
                zImagDD = nextImag;
                iterations++;
            }
        }

        return maxIterations;
    }
}
