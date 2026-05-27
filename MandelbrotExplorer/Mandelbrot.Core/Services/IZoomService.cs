using Mandelbrot.Core.Models;

namespace Mandelbrot.Core.Services;

public interface IZoomService
{
    Viewport CurrentViewport { get; }
    bool CanZoomOut { get; }

    void ZoomTo(ComplexPlane newPlane, int width, int height);
    void ZoomOut();
    void Reset(int width, int height);
}
