using FluentAssertions;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;
using Xunit;

namespace Mandelbrot.Tests.Core;

public class ZoomServiceTests
{
    [Fact]
    public void Reset_ShouldSetDefaultViewportAndClearHistory()
    {
        // Arrange
        var zoomService = new ZoomService();
        zoomService.ZoomTo(new ComplexPlane(0, 1, 0, 1), 100, 100);

        // Act
        zoomService.Reset(800, 600);

        // Assert
        zoomService.CanZoomOut.Should().BeFalse();
        zoomService.CurrentViewport.ImageWidth.Should().Be(800);
    }

    [Fact]
    public void ZoomTo_ShouldAddHistoryAndChangeViewport()
    {
        // Arrange
        var zoomService = new ZoomService();
        var initialViewport = zoomService.CurrentViewport;

        // Act
        var newPlane = new ComplexPlane(0, 1, 0, 1);
        zoomService.ZoomTo(newPlane, 400, 300);

        // Assert
        zoomService.CanZoomOut.Should().BeTrue();
        zoomService.CurrentViewport.Plane.Should().Be(newPlane);
        zoomService.CurrentViewport.ImageWidth.Should().Be(400);
        zoomService.CurrentViewport.ImageHeight.Should().Be(300);
    }

    [Fact]
    public void ZoomOut_ShouldRestorePreviousViewport()
    {
        // Arrange
        var zoomService = new ZoomService();
        zoomService.Reset(800, 600);
        var initialViewport = zoomService.CurrentViewport;

        zoomService.ZoomTo(new ComplexPlane(0, 1, 0, 1), 400, 300);

        // Act
        zoomService.ZoomOut();

        // Assert
        zoomService.CanZoomOut.Should().BeFalse();
        zoomService.CurrentViewport.Should().Be(initialViewport);
    }
}
