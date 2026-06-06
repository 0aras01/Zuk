using FluentAssertions;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Xunit;

namespace Fractal.Tests.Core;

public class PerturbationEngineTests
{
    [Fact]
    public void PrecalculateReferenceOrbit_ShouldComputeCorrectly()
    {
        // Arrange
        var settings = new FractalSettings(FractalType.Mandelbrot, 0.0, 0.0);
        DoubleDouble centerReal = -0.7;
        DoubleDouble centerImag = 0.0;
        int maxIterations = 100;

        // Act
        var (refRe, refIm, escapeIter) = PerturbationEngine.PrecalculateReferenceOrbit(
            centerReal, centerImag, maxIterations, settings);

        // Assert
        refRe.Should().NotBeNull();
        refIm.Should().NotBeNull();
        refRe.Length.Should().Be(maxIterations);
        refIm.Length.Should().Be(maxIterations);
        escapeIter.Should().BeGreaterThan(0);
        escapeIter.Should().BeLessThanOrEqualTo(maxIterations);
    }

    [Fact]
    public void PerturbedIterations_ShouldMatchStandardIterations()
    {
        // Arrange
        var settings = new FractalSettings(FractalType.Mandelbrot, 0.0, 0.0);
        DoubleDouble centerReal = -0.7;
        DoubleDouble centerImag = 0.1;
        int maxIterations = 100;

        // Precalculate reference orbit at center
        var (refRe, refIm, refEscapeIter) = PerturbationEngine.PrecalculateReferenceOrbit(
            centerReal, centerImag, maxIterations, settings);

        // Test point close to center
        DoubleDouble pixelReal = -0.71;
        DoubleDouble pixelImag = 0.09;

        // Act
        double standardIter = FractalCalculator.ComputeSmoothIterations(
            pixelReal, pixelImag, maxIterations, settings);
        
        double perturbedIter = PerturbationEngine.ComputeSmoothIterations(
            pixelReal, pixelImag, centerReal, centerImag, refRe, refIm, refEscapeIter, maxIterations, settings);

        // Assert
        // They should be very close (within 0.1 iterations or exact)
        perturbedIter.Should().BeApproximately(standardIter, 0.1);
    }
}
