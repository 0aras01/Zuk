# Original User Request

## Initial Request — 2026-06-06T07:21:24Z

# Teamwork Project Prompt

Implement 8 major new features for the existing FractalExplorer desktop application (Avalonia C#). The features are categorized into: Color Palette Editor, UI Overlays (Minimap, Orbit Path), Rendering Engine (3D Normal Map Shading, High-Res Export), and Advanced UX (GIF Export, Random Discover, Split View).

Working directory: c:\Users\Admin\source\repos\Zuk\FractalExplorer
Integrity mode: development

## Requirements

### R1. Color Palette System
Replace the hardcoded palette ID system with a `GradientPalette` model (JSON-based storage). Implement a UI editor with an interactive gradient bar, color stops (add/remove/edit), and 12 built-in aesthetic palettes. Add a "Color Cycling" toggle that animates the palette offset continuously without re-rendering the fractal.

### R2. UI Overlays (Minimap & Orbit)
- **Minimap**: Add a small floating window in the bottom-left showing the full Mandelbrot set with a rectangle indicating the current viewport. Update it whenever the viewport changes.
- **Orbit Visualization**: When enabled (via 'O' key), clicking on the fractal should trace and draw the iteration path (orbit) of that point as a polyline overlay.

### R3. Advanced Rendering (3D Lighting & HD Export)
- **Normal Map Shading**: Implement Lambertian 3D lighting as a post-processing step based on iteration gradients. Add UI sliders for Light Azimuth, Elevation, and Ambient strength.
- **High-Res Export**: Implement a tiled rendering service capable of generating 4K/8K images directly to disk without running out of memory, accompanied by a UI progress bar.

### R4. Advanced UX (GIF, Discover, Split View)
- **GIF Export**: Use the `AnimatedGif` NuGet package to record a sequence of zoom frames and export them as an animated GIF.
- **Discover**: Add an algorithm that searches the Mandelbrot boundary for high-variance areas and navigates to a random interesting location.
- **Split View**: Implement a dual-panel view to compare different fractals or palettes side-by-side. Include a checkbox to toggle between synchronous navigation (both sides move together) and independent navigation.

## Acceptance Criteria

### Palette System
- [ ] Application successfully loads and saves custom `GradientPalette` definitions to a JSON file.
- [ ] Color cycling animates smoothly without triggering the heavy `GenerateFractalAsync` pipeline.

### Overlays
- [ ] Minimap correctly displays the relative position of the current viewport during deep zooms.
- [ ] Orbit visualization draws a visible path connecting iteration points for a clicked pixel.

### Rendering
- [ ] 3D Lighting applies a noticeable embossed/shaded effect, reacting to slider changes.
- [ ] HD Export successfully writes a 4K image file without throwing OutOfMemory exceptions.

### UX Features
- [ ] GIF export produces a valid animated .gif file of a recorded sequence.
- [ ] Discover button reliably jumps to a non-empty, visually interesting boundary location.
- [ ] Split View allows comparing two different palettes, with both sync and async navigation working correctly.
