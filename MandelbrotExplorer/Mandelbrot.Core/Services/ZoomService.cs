using System.Collections.Generic;
using Mandelbrot.Core.Models;

namespace Mandelbrot.Core.Services;

public class ZoomService : IZoomService
{
    private readonly Stack<Viewport> _history = new Stack<Viewport>();
    private Viewport _currentViewport;

    public Viewport CurrentViewport => _currentViewport;

    public bool CanZoomOut => _history.Count > 0;

    public ZoomService()
    {
        // Initial dummy viewport, usually overwritten immediately by Reset() with actual dimensions
        _currentViewport = GetDefaultViewport(800, 600);
    }

    public void ZoomTo(ComplexPlane newPlane, int width, int height)
    {
        _history.Push(_currentViewport);
        _currentViewport = new Viewport(newPlane, width, height);
    }

    public void ZoomOut()
    {
        if (CanZoomOut)
        {
            _currentViewport = _history.Pop();
        }
    }

    public void Reset(int width, int height)
    {
        _history.Clear();
        _currentViewport = GetDefaultViewport(width, height);
    }

    private static Viewport GetDefaultViewport(int width, int height)
    {
        // The standard Mandelbrot set typically fits nicely within Real: [-2.0, 1.0], Imag: [-1.5, 1.5]
        return new Viewport(new ComplexPlane(-2.5, 1.0, -1.5, 1.5), width, height);
    }
}
