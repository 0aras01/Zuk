namespace Mandelbrot.Core.Models;

/// <summary>
/// Represents a rectangular region on the complex plane.
/// </summary>
public readonly record struct ComplexPlane(DoubleDouble RealMin, DoubleDouble RealMax, DoubleDouble ImagMin, DoubleDouble ImagMax);
