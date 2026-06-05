using Mandelbrot.Core.Models;

namespace Mandelbrot.Core.Services;

public interface IZoomService
{
    Viewport CurrentViewport { get; }
    bool CanZoomOut { get; }

    void ZoomTo(ComplexPlane newPlane, int width, int height);
    void ZoomOut(int currentWidth, int currentHeight);
    void Reset(int width, int height);

    /// <summary>
    /// Updates the current viewport dimensions in-place without pushing to history.
    /// Adjusts the complex plane to preserve aspect ratio.
    /// </summary>
    void ResizeCurrent(int width, int height);

    /// <summary>
    /// Replaces the current viewport's complex plane in-place without pushing to history
    /// or adjusting aspect ratio. Used during interactive pan drag.
    /// </summary>
    void UpdateCurrentPlane(ComplexPlane plane);
}
