using FluentAssertions;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;
using Xunit;

namespace Mandelbrot.Tests.Core;

public class CoordinateMapperTests
{
    [Fact]
    public void PixelToComplex_ShouldMapCorrectly()
    {
        // Arrange
        var plane = new ComplexPlane(-2.0, 2.0, -2.0, 2.0);
        var viewport = new Viewport(plane, 400, 400);

        // Act
        var center = CoordinateMapper.PixelToComplex(200, 200, viewport);
        var topLeft = CoordinateMapper.PixelToComplex(0, 0, viewport);
        var bottomRight = CoordinateMapper.PixelToComplex(400, 400, viewport);

        // Assert
        ((double)center.real).Should().BeApproximately(0.0, 0.001);
        ((double)center.imag).Should().BeApproximately(0.0, 0.001);

        ((double)topLeft.real).Should().BeApproximately(-2.0, 0.001);
        ((double)topLeft.imag).Should().BeApproximately(2.0, 0.001); // Inverted Y

        ((double)bottomRight.real).Should().BeApproximately(2.0, 0.001);
        ((double)bottomRight.imag).Should().BeApproximately(-2.0, 0.001);
    }

    [Fact]
    public void DoubleDouble_Math_ShouldBePrecise()
    {
        DoubleDouble a = 1e-15;
        DoubleDouble b = 1e-15;
        DoubleDouble c = a * b;
        ((double)c).Should().BeApproximately(1e-30, 1e-45);

        DoubleDouble sum = a + b;
        ((double)sum).Should().BeApproximately(2e-15, 1e-30);

        DoubleDouble big = -0.7436438870371587;
        DoubleDouble small = 1.05e-11;
        DoubleDouble bigSum = big + small;
        double expected = -0.7436438870371587 + 1.05e-11;
        ((double)bigSum).Should().BeApproximately(expected, 1e-25);
    }
}
