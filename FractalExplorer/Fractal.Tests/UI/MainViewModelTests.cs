using Avalonia;
using FluentAssertions;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Fractal.UI.ViewModels;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fractal.Tests.UI;

public class MainViewModelTests
{
    private static Mock<IFractalGenerator> CreateMockGenerator()
    {
        var mock = new Mock<IFractalGenerator>();
        mock.Setup(g => g.Name).Returns("Test Generator");
        mock.Setup(g => g.IsGpuAccelerated).Returns(false);
        mock.Setup(g => g.GenerateAsync(
                It.IsAny<Viewport>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<FractalSettings>(), It.IsAny<CancellationToken>()))
            .Returns<Viewport, int, int, FractalSettings, CancellationToken>(
                (v, _, _, _, _) => Task.FromResult(new byte[v.ImageWidth * v.ImageHeight * 4]));
        return mock;
    }

    [Fact]
    public void Selection_UpdatesRectangle()
    {
        // Arrange
        var mockGenerator = CreateMockGenerator();
        var mockZoomService = new Mock<IZoomService>();
        mockZoomService.Setup(z => z.CurrentViewport).Returns(new Viewport(new ComplexPlane(0, 1, 0, 1), 800, 600));

        var vm = new MainViewModel(
            new NavigationViewModel(mockZoomService.Object, new BookmarkService(), NullLogger<NavigationViewModel>.Instance),
            new DiagnosticsViewModel(NullLogger<DiagnosticsViewModel>.Instance),
            new RenderingViewModel(mockGenerator.Object, mockZoomService.Object, NullLogger<RenderingViewModel>.Instance),
            NullLogger<MainViewModel>.Instance);

        // Act
        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(50, 50));

        // Assert
        vm.IsSelecting.Should().BeTrue();
        vm.SelectionRectangle.Width.Should().Be(40);
        vm.SelectionRectangle.Height.Should().Be(40);
        vm.SelectionRectangle.X.Should().Be(10);
        vm.SelectionRectangle.Y.Should().Be(10);
    }

    [Fact]
    public void PointerReleased_ShouldCallZoomService()
    {
        // Arrange
        var mockGenerator = CreateMockGenerator();
        var mockZoomService = new Mock<IZoomService>();
        mockZoomService.Setup(z => z.CurrentViewport).Returns(new Viewport(new ComplexPlane(0, 100, 0, 100), 100, 100)); // 1 to 1 mapping

        var vm = new MainViewModel(
            new NavigationViewModel(mockZoomService.Object, new BookmarkService(), NullLogger<NavigationViewModel>.Instance),
            new DiagnosticsViewModel(NullLogger<DiagnosticsViewModel>.Instance),
            new RenderingViewModel(mockGenerator.Object, mockZoomService.Object, NullLogger<RenderingViewModel>.Instance),
            NullLogger<MainViewModel>.Instance);
        vm.ViewportWidth = 100;
        vm.ViewportHeight = 100;

        vm.OnPointerPressed(new Point(10, 10));
        vm.OnPointerMoved(new Point(50, 50));

        // Act
        vm.OnPointerReleased(new Point(50, 50));

        // Assert
        mockZoomService.Verify(z => z.ZoomTo(It.IsAny<ComplexPlane>(), 100, 100), Times.Once);
        vm.IsSelecting.Should().BeFalse();
    }

    [Fact]
    public void OnSizeChanged_ShouldCallResizeCurrentInsteadOfZoomTo()
    {
        // Arrange
        var mockGenerator = CreateMockGenerator();
        var mockZoomService = new Mock<IZoomService>();
        mockZoomService.Setup(z => z.CurrentViewport).Returns(new Viewport(new ComplexPlane(-2, 1, -1, 1), 800, 600));

        var vm = new MainViewModel(
            new NavigationViewModel(mockZoomService.Object, new BookmarkService(), NullLogger<NavigationViewModel>.Instance),
            new DiagnosticsViewModel(NullLogger<DiagnosticsViewModel>.Instance),
            new RenderingViewModel(mockGenerator.Object, mockZoomService.Object, NullLogger<RenderingViewModel>.Instance),
            NullLogger<MainViewModel>.Instance);

        // Act
        vm.OnSizeChanged(1024, 768);

        // Assert — should call ResizeCurrent, not ZoomTo
        mockZoomService.Verify(z => z.ResizeCurrent(1024, 768), Times.Once);
        mockZoomService.Verify(z => z.ZoomTo(It.IsAny<ComplexPlane>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}

