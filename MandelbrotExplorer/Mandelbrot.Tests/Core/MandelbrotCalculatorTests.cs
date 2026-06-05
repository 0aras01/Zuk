using FluentAssertions;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;
using Xunit;

namespace Mandelbrot.Tests.Core;

public class MandelbrotCalculatorTests
{
    [Fact]
    public void ComputeIterations_ShouldMatchExpectations()
    {
        // Inside set
        MandelbrotCalculator.ComputeIterations(0.0, 0.0, 100).Should().Be(100);
        
        // Clearly outside
        MandelbrotCalculator.ComputeIterations(2.0, 2.0, 100).Should().BeLessThan(10);
    }

    [Fact]
    public void ComputeSmoothIterations_ShouldBeContinuous()
    {
        double inside = MandelbrotCalculator.ComputeSmoothIterations(0.0, 0.0, 100);
        inside.Should().Be(100.0);

        double outside = MandelbrotCalculator.ComputeSmoothIterations(0.5, 0.5, 100);
        outside.Should().BeLessThan(100.0);
        
        // Smooth iterations should be fractional
        (outside % 1.0).Should().NotBe(0.0);
    }

    [Theory]
    [InlineData(1)] // Sunset (Fire)
    [InlineData(2)] // Ice (Blue)
    [InlineData(3)] // Rainbow
    [InlineData(4)] // Forest
    public void GetColor_ShouldReturnValidRgb(int paletteId)
    {
        for (double t = 0.0; t <= 1.0; t += 0.1)
        {
            MandelbrotCalculator.GetColor(t, paletteId, out byte r, out byte g, out byte b);
            r.Should().BeInRange(0, 255);
            g.Should().BeInRange(0, 255);
            b.Should().BeInRange(0, 255);
        }
    }
}
