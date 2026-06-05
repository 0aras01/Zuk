using FluentAssertions;
using Mandelbrot.Core.Models;
using Xunit;

namespace Mandelbrot.Tests.Core;

public class DoubleDoubleTests
{
    [Fact]
    public void Addition_ShouldBePrecise()
    {
        DoubleDouble a = 1.0;
        DoubleDouble b = 1e-16;
        DoubleDouble sum = a + b;
        
        sum.Hi.Should().Be(1.0);
        sum.Lo.Should().Be(1e-16);
    }

    [Fact]
    public void Subtraction_ShouldBePrecise()
    {
        DoubleDouble a = 1.0;
        DoubleDouble b = 1e-16;
        DoubleDouble diff = a - b;

        // The closest double to 1.0 - 1e-16 is 0.9999999999999999
        diff.Hi.Should().Be(0.9999999999999999);
        diff.Lo.Should().BeApproximately(1.10223e-17, 1e-21);
    }

    [Fact]
    public void Multiplication_ShouldBePrecise()
    {
        DoubleDouble a = 2.0;
        DoubleDouble b = 1.5e-16;
        DoubleDouble prod = a * b;

        prod.Hi.Should().Be(3e-16);
        prod.Lo.Should().Be(0.0);
    }

    [Fact]
    public void Comparison_ShouldBeCorrect()
    {
        DoubleDouble a = new DoubleDouble(1.0, 1e-16);
        DoubleDouble b = new DoubleDouble(1.0, 0.0);
        DoubleDouble c = new DoubleDouble(1.0, -1e-16);

        (a > b).Should().BeTrue();
        (b > c).Should().BeTrue();
        (c < b).Should().BeTrue();
        (b < a).Should().BeTrue();
        (a > 0.5).Should().BeTrue();
        (c < 1.5).Should().BeTrue();
    }

    [Fact]
    public void MinMax_ShouldBeCorrect()
    {
        DoubleDouble a = new DoubleDouble(1.0, 1e-16);
        DoubleDouble b = new DoubleDouble(1.0, -1e-16);

        DoubleDouble.Min(a, b).Should().Be(b);
        DoubleDouble.Max(a, b).Should().Be(a);
    }
}
