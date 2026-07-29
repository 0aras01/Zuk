using System;
using System.Runtime.CompilerServices;

namespace Fractal.Core.Models;

public struct FractalParams
{
    public int Width;
    public int Height;
    public int MaxIterations;

    public DoubleDouble RealMin;
    public DoubleDouble RealMax;
    public DoubleDouble ImagMin;
    public DoubleDouble ImagMax;

    public DoubleDouble JuliaCReal;
    public DoubleDouble JuliaCImag;
}
