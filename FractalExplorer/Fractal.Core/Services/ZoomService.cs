using System;
using System.Collections.Generic;
using Fractal.Core.Models;

namespace Fractal.Core.Services;

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
        var adjusted = AdjustAspectRatio(newPlane, width, height);
        _currentViewport = new Viewport(adjusted, width, height);
    }

    public void ZoomOut(int currentWidth, int currentHeight)
    {
        if (CanZoomOut)
        {
            var previous = _history.Pop();
            // Adapt the previous plane to the current window dimensions
            var adjusted = AdjustAspectRatio(previous.Plane, currentWidth, currentHeight);
            _currentViewport = new Viewport(adjusted, currentWidth, currentHeight);
        }
    }

    public void Reset(int width, int height)
    {
        _history.Clear();
        _currentViewport = GetDefaultViewport(width, height);
    }

    public void ResizeCurrent(int width, int height)
    {
        // Update the current viewport in-place without pushing to history.
        // Adjust the complex plane to preserve aspect ratio for the new dimensions.
        var adjusted = AdjustAspectRatio(_currentViewport.Plane, width, height);
        _currentViewport = new Viewport(adjusted, width, height);
    }

    public void UpdateCurrentPlane(ComplexPlane plane)
    {
        _currentViewport = new Viewport(plane, _currentViewport.ImageWidth, _currentViewport.ImageHeight);
    }

    private static Viewport GetDefaultViewport(int width, int height)
    {
        // The standard Mandelbrot set typically fits nicely within Real: [-2.5, 1.0], Imag: [-1.5, 1.5]
        var basePlane = new ComplexPlane(-2.5, 1.0, -1.5, 1.5);
        var adjusted = AdjustAspectRatio(basePlane, width, height);
        return new Viewport(adjusted, width, height);
    }

    /// <summary>
    /// Adjusts the complex plane boundaries so that the aspect ratio of the plane
    /// matches the aspect ratio of the pixel viewport, expanding the shorter axis
    /// to prevent stretching.
    /// </summary>
    internal static ComplexPlane AdjustAspectRatio(ComplexPlane plane, int width, int height)
    {
        if (width <= 0 || height <= 0) return plane;

        DoubleDouble planeWidth = plane.RealMax - plane.RealMin;
        DoubleDouble planeHeight = plane.ImagMax - plane.ImagMin;

        double viewportAspect = (double)width / height;
        double planeAspect = (double)planeWidth / (double)planeHeight;

        DoubleDouble realMin = plane.RealMin;
        DoubleDouble realMax = plane.RealMax;
        DoubleDouble imagMin = plane.ImagMin;
        DoubleDouble imagMax = plane.ImagMax;

        if (viewportAspect > planeAspect)
        {
            // Viewport is wider than the plane — expand the real (horizontal) axis
            DoubleDouble newPlaneWidth = planeHeight * viewportAspect;
            DoubleDouble centerReal = (realMin + realMax) * 0.5;
            realMin = centerReal - newPlaneWidth * 0.5;
            realMax = centerReal + newPlaneWidth * 0.5;
        }
        else if (viewportAspect < planeAspect)
        {
            // Viewport is taller than the plane — expand the imaginary (vertical) axis
            DoubleDouble newPlaneHeight = planeWidth / viewportAspect;
            DoubleDouble centerImag = (imagMin + imagMax) * 0.5;
            imagMin = centerImag - newPlaneHeight * 0.5;
            imagMax = centerImag + newPlaneHeight * 0.5;
        }

        return new ComplexPlane(realMin, realMax, imagMin, imagMax);
    }
}

