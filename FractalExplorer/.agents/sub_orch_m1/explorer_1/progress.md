# Progress

Last visited: 2026-06-06T09:28:00+02:00

- Discovered current hardcoded palette in `FractalCalculator.cs` and `PaletteType.cs`.
- Traced generation path through `ILGPUFractalGenerator.cs` and `ParallelFractalGenerator.cs`.
- Discovered that generators currently map iterations to colors internally.
- Designed a strategy to split generation into Iterations Output (`float[]`) and Color Mapping to support color cycling without re-rendering.
- Wrote `handoff.md` with a detailed step-by-step implementation plan.
