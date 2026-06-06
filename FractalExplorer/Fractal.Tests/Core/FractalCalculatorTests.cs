using FluentAssertions;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Xunit;

namespace Fractal.Tests.Core;

public class FractalCalculatorTests
{
    [Fact]
    public void ComputeIterations_ShouldMatchExpectations()
    {
        // Inside set
        FractalCalculator.ComputeIterations(0.0, 0.0, 100).Should().Be(100);
        
        // Clearly outside
        FractalCalculator.ComputeIterations(2.0, 2.0, 100).Should().BeLessThan(10);
    }

    [Fact]
    public void ComputeSmoothIterations_ShouldBeContinuous()
    {
        double inside = FractalCalculator.ComputeSmoothIterations(0.0, 0.0, 100);
        inside.Should().Be(100.0);

        double outside = FractalCalculator.ComputeSmoothIterations(0.5, 0.5, 100);
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
            FractalCalculator.GetColor(t, paletteId, out byte r, out byte g, out byte b);
            r.Should().BeInRange(0, 255);
            g.Should().BeInRange(0, 255);
            b.Should().BeInRange(0, 255);
        }
    }

    [Fact]
    public void ComputeIterations_Julia_ShouldMatchExpectations()
    {
        // Julia set with C = -0.7 + 0.27015i
        var settings = new FractalSettings(FractalType.Julia, -0.7, 0.27015);

        // Point near origin should stay bounded or escape very slowly
        FractalCalculator.ComputeIterations(0.0, 0.0, 100, settings).Should().BeGreaterThan(50);

        // Point far out should escape
        FractalCalculator.ComputeIterations(2.0, 2.0, 100, settings).Should().BeLessThan(10);
    }

    [Fact]
    public void ComputeIterations_BurningShip_ShouldMatchExpectations()
    {
        var settings = new FractalSettings(FractalType.BurningShip, 0.0, 0.0);

        // Origin should stay inside
        FractalCalculator.ComputeIterations(0.0, 0.0, 100, settings).Should().Be(100);

        // Far out should escape
        FractalCalculator.ComputeIterations(2.0, 2.0, 100, settings).Should().BeLessThan(10);
    }

    [Fact]
    public void ComputeIterations_Tricorn_ShouldMatchExpectations()
    {
        var settings = new FractalSettings(FractalType.Tricorn, 0.0, 0.0);
        FractalCalculator.ComputeIterations(0.0, 0.0, 100, settings).Should().Be(100);
        FractalCalculator.ComputeIterations(2.0, 2.0, 100, settings).Should().BeLessThan(10);
    }

    [Fact]
    public void ComputeIterations_Celtic_ShouldMatchExpectations()
    {
        var settings = new FractalSettings(FractalType.Celtic, 0.0, 0.0);
        FractalCalculator.ComputeIterations(0.0, 0.0, 100, settings).Should().Be(100);
        FractalCalculator.ComputeIterations(2.0, 2.0, 100, settings).Should().BeLessThan(10);
    }

    [Fact]
    public void ComputeIterations_Buffalo_ShouldMatchExpectations()
    {
        var settings = new FractalSettings(FractalType.Buffalo, 0.0, 0.0);
        FractalCalculator.ComputeIterations(0.0, 0.0, 100, settings).Should().Be(100);
        FractalCalculator.ComputeIterations(2.0, 2.0, 100, settings).Should().BeLessThan(10);
    }

    [Fact]
    public void ComputeIterations_Multibrot3_ShouldMatchExpectations()
    {
        var settings = new FractalSettings(FractalType.Multibrot3, 0.0, 0.0);
        FractalCalculator.ComputeIterations(0.0, 0.0, 100, settings).Should().Be(100);
        FractalCalculator.ComputeIterations(2.0, 2.0, 100, settings).Should().BeLessThan(10);
    }
}

