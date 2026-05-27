using Mandelbrot.Core.Models;

namespace Mandelbrot.Core.Services;

/// <summary>
/// Provides methods to map between pixel coordinates and complex plane coordinates.
/// </summary>
public static class CoordinateMapper
{
    public static (double real, double imag) PixelToComplex(int x, int y, Viewport viewport)
    {
        double realRange = viewport.Plane.RealMax - viewport.Plane.RealMin;
        double imagRange = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

        double real = viewport.Plane.RealMin + (x * realRange / viewport.ImageWidth);
        double imag = viewport.Plane.ImagMax - (y * imagRange / viewport.ImageHeight); // Inverted Y-axis

        return (real, imag);
    }

    public static (int x, int y) ComplexToPixel(double real, double imag, Viewport viewport)
    {
        double realRange = viewport.Plane.RealMax - viewport.Plane.RealMin;
        double imagRange = viewport.Plane.ImagMax - viewport.Plane.ImagMin;

        int x = (int)((real - viewport.Plane.RealMin) * viewport.ImageWidth / realRange);
        int y = (int)((viewport.Plane.ImagMax - imag) * viewport.ImageHeight / imagRange);

        return (x, y);
    }
}
