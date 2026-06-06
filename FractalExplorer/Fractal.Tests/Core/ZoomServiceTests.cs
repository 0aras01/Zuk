using FluentAssertions;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Xunit;

namespace Fractal.Tests.Core;

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
        zoomService.ZoomOut(800, 600);

        // Assert
        zoomService.CanZoomOut.Should().BeFalse();
        // Plane center should match the initial plane center
        var plane = zoomService.CurrentViewport.Plane;
        var initialPlane = initialViewport.Plane;
        double centerReal = (double)(plane.RealMin + plane.RealMax) / 2.0;
        double initialCenterReal = (double)(initialPlane.RealMin + initialPlane.RealMax) / 2.0;
        centerReal.Should().BeApproximately(initialCenterReal, 0.001);
    }

    [Fact]
    public void ResizeCurrent_ShouldNotPushToHistory()
    {
        // Arrange
        var zoomService = new ZoomService();
        zoomService.Reset(800, 600);

        // Act — resize multiple times
        zoomService.ResizeCurrent(1024, 768);
        zoomService.ResizeCurrent(1200, 900);
        zoomService.ResizeCurrent(640, 480);

        // Assert — no entries should have been pushed
        zoomService.CanZoomOut.Should().BeFalse();
        zoomService.CurrentViewport.ImageWidth.Should().Be(640);
        zoomService.CurrentViewport.ImageHeight.Should().Be(480);
    }

    [Fact]
    public void ZoomTo_ShouldPreserveAspectRatio()
    {
        // Arrange
        var zoomService = new ZoomService();
        zoomService.Reset(800, 400); // 2:1 aspect ratio

        // Act — zoom into a square region on the complex plane
        var squarePlane = new ComplexPlane(-1, 1, -1, 1); // 1:1 aspect
        zoomService.ZoomTo(squarePlane, 800, 400);

        // Assert — the plane should be expanded horizontally to match 2:1 viewport
        var plane = zoomService.CurrentViewport.Plane;
        double planeWidth = (double)(plane.RealMax - plane.RealMin);
        double planeHeight = (double)(plane.ImagMax - plane.ImagMin);
        double planeAspect = planeWidth / planeHeight;
        planeAspect.Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public void AdjustAspectRatio_WiderViewport_ExpandsRealAxis()
    {
        // Arrange — square plane, wide viewport
        var plane = new ComplexPlane(-1, 1, -1, 1);

        // Act
        var adjusted = ZoomService.AdjustAspectRatio(plane, 800, 400);

        // Assert
        double width = (double)(adjusted.RealMax - adjusted.RealMin);
        double height = (double)(adjusted.ImagMax - adjusted.ImagMin);
        (width / height).Should().BeApproximately(2.0, 0.001);
        // Center should be preserved
        ((double)(adjusted.RealMin + adjusted.RealMax) / 2.0).Should().BeApproximately(0.0, 0.001);
        ((double)(adjusted.ImagMin + adjusted.ImagMax) / 2.0).Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void AdjustAspectRatio_TallerViewport_ExpandsImagAxis()
    {
        // Arrange — square plane, tall viewport
        var plane = new ComplexPlane(-1, 1, -1, 1);

        // Act
        var adjusted = ZoomService.AdjustAspectRatio(plane, 400, 800);

        // Assert
        double width = (double)(adjusted.RealMax - adjusted.RealMin);
        double height = (double)(adjusted.ImagMax - adjusted.ImagMin);
        (width / height).Should().BeApproximately(0.5, 0.001);
        // Center should be preserved
        ((double)(adjusted.RealMin + adjusted.RealMax) / 2.0).Should().BeApproximately(0.0, 0.001);
        ((double)(adjusted.ImagMin + adjusted.ImagMax) / 2.0).Should().BeApproximately(0.0, 0.001);
    }
}

