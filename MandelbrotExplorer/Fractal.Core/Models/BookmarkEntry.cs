namespace Fractal.Core.Models;

public class BookmarkEntry
{
    public string Name { get; set; } = string.Empty;
    public FractalType FractalType { get; set; }
    public ComplexPlane Plane { get; set; }
    public PaletteType Palette { get; set; }
    public int Iterations { get; set; }
    public double JuliaCReal { get; set; }
    public double JuliaCImag { get; set; }
}
