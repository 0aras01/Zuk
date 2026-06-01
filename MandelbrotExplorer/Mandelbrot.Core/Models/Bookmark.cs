namespace Mandelbrot.Core.Models;

public record Bookmark(string Name, ComplexPlane Plane, int MaxIterations, Enums.ColorTheme Theme);
