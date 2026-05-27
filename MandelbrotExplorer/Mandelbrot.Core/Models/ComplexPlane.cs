namespace Mandelbrot.Core.Models;

/// <summary>
/// Represents a rectangular region on the complex plane.
/// </summary>
public readonly record struct ComplexPlane(double RealMin, double RealMax, double ImagMin, double ImagMax);
