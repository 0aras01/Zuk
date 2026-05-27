using Avalonia;
using FluentAssertions;
using Mandelbrot.Core.Models;
using Mandelbrot.Core.Services;
using Mandelbrot.UI.ViewModels;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mandelbrot.Tests.UI;

public class MainViewModelTests
{
    [Fact]
    public void Selection_UpdatesRectangle()
    {
        // Arrange
        var mockGenerator = new Mock<IFractalGenerator>();
        var mockZoomService = new Mock<IZoomService>();
        mockZoomService.Setup(z => z.CurrentViewport).Returns(new Viewport(new ComplexPlane(0, 1, 0, 1), 800, 600));

        var vm = new MainViewModel(mockGenerator.Object, mockZoomService.Object);

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
        var mockGenerator = new Mock<IFractalGenerator>();
        var mockZoomService = new Mock<IZoomService>();
        mockZoomService.Setup(z => z.CurrentViewport).Returns(new Viewport(new ComplexPlane(0, 100, 0, 100), 100, 100)); // 1 to 1 mapping

        var vm = new MainViewModel(mockGenerator.Object, mockZoomService.Object);
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
}
