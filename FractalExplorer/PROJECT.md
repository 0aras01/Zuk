# Project: FractalExplorer New Features

## Architecture
This project adds 8 major new features to the existing FractalExplorer desktop application (Avalonia C#).
The features are categorized into four milestones:
- M1: Color Palette System (GradientPalette model, UI editor, Color Cycling)
- M2: UI Overlays (Minimap, Orbit Path)
- M3: Advanced Rendering (3D Normal Map Shading, High-Res Export)
- M4: Advanced UX (GIF Export, Random Discover, Split View)

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Color Palette System | Replace hardcoded palette ID system with a `GradientPalette` model (JSON-based storage). Implement a UI editor with an interactive gradient bar, color stops, and 12 built-in aesthetic palettes. Add a "Color Cycling" toggle that animates the palette offset continuously without re-rendering the fractal. | None | PLANNED |
| 2 | UI Overlays | Add a small floating window in the bottom-left showing the full Mandelbrot set with a rectangle indicating the current viewport. Add Orbit Visualization (toggled via 'O' key), clicking on the fractal traces and draws the iteration path as a polyline overlay. | None | PLANNED |
| 3 | Advanced Rendering | Implement Lambertian 3D lighting as a post-processing step based on iteration gradients. Add UI sliders for Light Azimuth, Elevation, and Ambient strength. Implement a tiled rendering service capable of generating 4K/8K images directly to disk without running out of memory, accompanied by a UI progress bar. | None | PLANNED |
| 4 | Advanced UX | Use AnimatedGif NuGet package to record a sequence of zoom frames and export them as an animated GIF. Add an algorithm that searches the Mandelbrot boundary for high-variance areas and navigates to a random interesting location. Implement a dual-panel view to compare different fractals or palettes side-by-side with a sync/async navigation toggle. | None | PLANNED |

## Interface Contracts
TBD by each sub-orchestrator.

## Code Layout
Existing codebase.
