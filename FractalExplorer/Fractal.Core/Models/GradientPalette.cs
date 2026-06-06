namespace Fractal.Core.Models;

/// <summary>
/// A single color stop in a gradient palette.
/// Position is in range [0.0, 1.0].
/// </summary>
public record GradientStop(double Position, byte R, byte G, byte B);

/// <summary>
/// A gradient-based color palette defined by a list of color stops.
/// Supports both built-in and user-created palettes.
/// </summary>
public class GradientPalette
{
    public string Name { get; set; } = string.Empty;
    public List<GradientStop> Stops { get; set; } = new();
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Interpolates the gradient at position t ∈ [0, 1] with an optional offset and returns RGB color.
    /// </summary>
    public void GetColor(double t, double offset, out byte r, out byte g, out byte b)
    {
        if (Stops.Count == 0)
        {
            r = g = b = 0;
            return;
        }

        if (Stops.Count == 1)
        {
            r = Stops[0].R;
            g = Stops[0].G;
            b = Stops[0].B;
            return;
        }

        // Apply offset and wrap around
        t = (t + offset) - Math.Floor(t + offset);

        // Clamp t to [0, 1] just in case
        t = Math.Clamp(t, 0.0, 1.0);

        // Find the two stops surrounding t
        int i = 0;
        for (; i < Stops.Count - 1; i++)
        {
            if (Stops[i + 1].Position >= t) break;
        }

        if (i >= Stops.Count - 1)
        {
            var last = Stops[^1];
            r = last.R; g = last.G; b = last.B;
            return;
        }

        var s0 = Stops[i];
        var s1 = Stops[i + 1];

        double range = s1.Position - s0.Position;
        double blend = range > 0 ? (t - s0.Position) / range : 0.0;
        blend = Math.Clamp(blend, 0.0, 1.0);

        r = (byte)(s0.R + (s1.R - s0.R) * blend);
        g = (byte)(s0.G + (s1.G - s0.G) * blend);
        b = (byte)(s0.B + (s1.B - s0.B) * blend);
    }
}
