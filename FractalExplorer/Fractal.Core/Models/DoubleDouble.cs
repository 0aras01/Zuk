using System;
using System.Runtime.CompilerServices;

namespace Fractal.Core.Models;

public readonly struct DoubleDouble : IEquatable<DoubleDouble>
{

    public static readonly DoubleDouble Zero = new DoubleDouble(0.0, 0.0);
    public static readonly DoubleDouble One = new DoubleDouble(1.0, 0.0);
    public static readonly DoubleDouble Two = new DoubleDouble(2.0, 0.0);
    public static readonly DoubleDouble Four = new DoubleDouble(4.0, 0.0);

    public readonly double Hi;
    public readonly double Lo;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DoubleDouble(double hi, double lo)
    {
        Hi = hi;
        Lo = lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble FromDouble(double value)
    {
        return new DoubleDouble(value, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DoubleDouble(double value)
    {
        return new DoubleDouble(value, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator double(DoubleDouble value)
    {
        return value.Hi;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator +(DoubleDouble a, DoubleDouble b)
    {
        double s = a.Hi + b.Hi;
        double v = s - a.Hi;
        double e = (a.Hi - (s - v)) + (b.Hi - v);
        e += a.Lo + b.Lo;
        
        double rHi = s + e;
        double rLo = e - (rHi - s);
        return new DoubleDouble(rHi, rLo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator +(DoubleDouble a, double b)
    {
        return a + new DoubleDouble(b, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator -(DoubleDouble a)
    {
        return new DoubleDouble(-a.Hi, -a.Lo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator -(DoubleDouble a, DoubleDouble b)
    {
        double s = a.Hi - b.Hi;
        double v = s - a.Hi;
        double e = (a.Hi - (s - v)) - (b.Hi + v);
        e += a.Lo - b.Lo;

        double rHi = s + e;
        double rLo = e - (rHi - s);
        return new DoubleDouble(rHi, rLo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator -(DoubleDouble a, double b)
    {
        return a + new DoubleDouble(-b, 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator *(DoubleDouble a, DoubleDouble b)
    {
        double p1 = a.Hi * b.Hi;
        
        // Split a.Hi
        double cA = 134217729.0 * a.Hi;
        double abTemp = cA - a.Hi;
        double aHi = cA - abTemp;
        double aLo = a.Hi - aHi;
        
        // Split b.Hi
        double cB = 134217729.0 * b.Hi;
        double bbTemp = cB - b.Hi;
        double bHi = cB - bbTemp;
        double bLo = b.Hi - bHi;
        
        double p2 = ((aHi * bHi - p1) + aHi * bLo + aLo * bHi) + aLo * bLo;
        p2 += a.Hi * b.Lo + a.Lo * b.Hi;
        
        double rHi = p1 + p2;
        double rLo = p2 - (rHi - p1);
        return new DoubleDouble(rHi, rLo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator *(DoubleDouble a, double b)
    {
        return a * new DoubleDouble(b, 0.0);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator /(DoubleDouble a, DoubleDouble b)
    {
        double q1 = a.Hi / b.Hi;
        DoubleDouble r = a - b * q1;
        double q2 = r.Hi / b.Hi;

        double rHi = q1 + q2;
        double rLo = q2 - (rHi - q1);
        return new DoubleDouble(rHi, rLo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator /(DoubleDouble a, double b)
    {
        double q1 = a.Hi / b;
        double q2 = a.Lo / b;

        double rHi = q1 + q2;
        double rLo = q2 - (rHi - q1);
        return new DoubleDouble(rHi, rLo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(DoubleDouble a, double b)
    {
        return a.Hi > b || (a.Hi == b && a.Lo > 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(DoubleDouble a, double b)
    {
        return a.Hi < b || (a.Hi == b && a.Lo < 0.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(DoubleDouble a, DoubleDouble b)
    {
        return a.Hi > b.Hi || (a.Hi == b.Hi && a.Lo > b.Lo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(DoubleDouble a, DoubleDouble b)
    {
        return a.Hi < b.Hi || (a.Hi == b.Hi && a.Lo < b.Lo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble Min(DoubleDouble a, DoubleDouble b)
    {
        return a < b ? a : b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble Max(DoubleDouble a, DoubleDouble b)
    {
        return a > b ? a : b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DoubleDouble Abs()
    {
        if (Hi < 0.0 || (Hi == 0.0 && Lo < 0.0))
            return -this;
        return this;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(DoubleDouble a, DoubleDouble b) => a.Hi > b.Hi || (a.Hi == b.Hi && a.Lo >= b.Lo);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(DoubleDouble a, DoubleDouble b) => a.Hi < b.Hi || (a.Hi == b.Hi && a.Lo <= b.Lo);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(DoubleDouble a, double b) => a.Hi > b || (a.Hi == b && a.Lo >= 0.0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(DoubleDouble a, double b) => a.Hi < b || (a.Hi == b && a.Lo <= 0.0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(DoubleDouble a, DoubleDouble b) => a.Hi == b.Hi && a.Lo == b.Lo;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(DoubleDouble a, DoubleDouble b) => a.Hi != b.Hi || a.Lo != b.Lo;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(DoubleDouble a, double b) => a.Hi == b && a.Lo == 0.0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(DoubleDouble a, double b) => a.Hi != b || a.Lo != 0.0;

    public override bool Equals(object? obj) => obj is DoubleDouble other && this == other;

    public bool Equals(DoubleDouble other) => this == other;

    public override int GetHashCode() => HashCode.Combine(Hi, Lo);

    public override string ToString()
    {
        return (Hi + Lo).ToString();
    }

    public string ToFullString()
    {
        if (Math.Abs(Lo) < 1e-30)
            return Hi.ToString("G17");
        return $"{Hi:G17} ({Lo:+#.####e+00;-#.####e+00;+0})";
    }
}

