import re

with open('FractalExplorer/Fractal.Core/Models/DoubleDouble.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Add constants
constants = """
    public static readonly DoubleDouble Zero = new DoubleDouble(0.0, 0.0);
    public static readonly DoubleDouble One = new DoubleDouble(1.0, 0.0);
    public static readonly DoubleDouble Two = new DoubleDouble(2.0, 0.0);
    public static readonly DoubleDouble Four = new DoubleDouble(4.0, 0.0);
"""
# insert constants after the struct declaration
content = re.sub(r'(public readonly struct DoubleDouble\s*\{)', r'\1\n' + constants, content)

# Update Abs method
abs_old = """    public DoubleDouble Abs()
    {
        return Hi < 0.0 ? -this : this;
    }"""
abs_new = """    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DoubleDouble Abs()
    {
        if (Hi < 0.0 || (Hi == 0.0 && Lo < 0.0))
            return -this;
        return this;
    }"""
content = content.replace(abs_old, abs_new)

# Update double division operator
div_double_old = """    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator /(DoubleDouble a, double b)
    {
        return a * (1.0 / b);
    }"""
div_double_new = """    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator /(DoubleDouble a, double b)
    {
        double q1 = a.Hi / b;
        double q2 = a.Lo / b;

        double rHi = q1 + q2;
        double rLo = q2 - (rHi - q1);
        return new DoubleDouble(rHi, rLo);
    }"""
content = content.replace(div_double_old, div_double_new)

# Update DoubleDouble subtraction operator
sub_dd_old = """    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator -(DoubleDouble a, DoubleDouble b)
    {
        return a + (-b);
    }"""
sub_dd_new = """    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleDouble operator -(DoubleDouble a, DoubleDouble b)
    {
        double s = a.Hi - b.Hi;
        double v = s - a.Hi;
        double e = (a.Hi - (s - v)) - (b.Hi + v);
        e += a.Lo - b.Lo;

        double rHi = s + e;
        double rLo = e - (rHi - s);
        return new DoubleDouble(rHi, rLo);
    }"""
content = content.replace(sub_dd_old, sub_dd_new)

# Add operator /(DoubleDouble, DoubleDouble)
div_dd = """
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
"""
content = content.replace(div_double_new, div_dd + "\n" + div_double_new)

# Add operator >= and <= for DoubleDouble and double
cmp = """
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

    public override bool Equals(object obj) => obj is DoubleDouble other && this == other;

    public bool Equals(DoubleDouble other) => this == other;

    public override int GetHashCode() => HashCode.Combine(Hi, Lo);
"""

# Append IEquatable to struct definition
content = content.replace("public readonly struct DoubleDouble", "public readonly struct DoubleDouble : IEquatable<DoubleDouble>")
content = content.replace("    public override string ToString()", cmp + "\n    public override string ToString()")

with open('FractalExplorer/Fractal.Core/Models/DoubleDouble.cs', 'w', encoding='utf-8') as f:
    f.write(content)
