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
        center.real.Should().BeApproximately(0.0, 0.001);
        center.imag.Should().BeApproximately(0.0, 0.001);

        topLeft.real.Should().BeApproximately(-2.0, 0.001);
        topLeft.imag.Should().BeApproximately(2.0, 0.001); // Inverted Y

        bottomRight.real.Should().BeApproximately(2.0, 0.001);
        bottomRight.imag.Should().BeApproximately(-2.0, 0.001);
    }
}
