namespace Fractal.Core.Models;

/// <summary>
/// Represents the full state of the viewport, including the mathematical region and the pixel dimensions.
/// </summary>
public readonly record struct Viewport(ComplexPlane Plane, int ImageWidth, int ImageHeight);

