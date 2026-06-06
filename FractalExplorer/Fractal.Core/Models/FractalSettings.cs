namespace Fractal.Core.Models;

public readonly record struct FractalSettings(
    FractalType Type,
    DoubleDouble JuliaCReal,
    DoubleDouble JuliaCImag
);
