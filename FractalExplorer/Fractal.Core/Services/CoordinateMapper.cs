using Fractal.Core.Models;

namespace Fractal.Core.Services;

/// <summary>
/// Provides methods to map between pixel coordinates and complex plane coordinates.
/// </summary>
public static class CoordinateMapper
{
    public static (DoubleDouble real, DoubleDouble imag) PixelToComplex(int x, int y, Viewport viewport)
    {
        DoubleDouble realRange = viewport.Plane.RealMax - viewport.Plane.RealMin;
        DoubleDouble imagRange = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

        DoubleDouble real = viewport.Plane.RealMin + (realRange * x / viewport.ImageWidth);
        DoubleDouble imag = viewport.Plane.ImagMax - (imagRange * y / viewport.ImageHeight); // Inverted Y-axis

        return (real, imag);
    }

    public static (int x, int y) ComplexToPixel(DoubleDouble real, DoubleDouble imag, Viewport viewport)
    {
        DoubleDouble realRange = viewport.Plane.RealMax - viewport.Plane.RealMin;
        DoubleDouble imagRange = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

        int x = (int)((double)(real - viewport.Plane.RealMin) * viewport.ImageWidth / (double)realRange);
        int y = (int)((double)(viewport.Plane.ImagMax - imag) * viewport.ImageHeight / (double)imagRange);

        return (x, y);
    }
}

