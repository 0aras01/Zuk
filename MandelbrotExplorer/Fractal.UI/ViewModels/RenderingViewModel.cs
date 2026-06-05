using CommunityToolkit.Mvvm.ComponentModel;
using Fractal.Core.Services;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public class RenderingViewModel : ObservableObject
{
    private readonly IFractalGenerator? _fractalGenerator;
    private readonly IZoomService? _zoomService;
    private readonly ILogger<RenderingViewModel>? _logger;

    public RenderingViewModel()
    {
    }

    public RenderingViewModel(IFractalGenerator fractalGenerator, IZoomService zoomService, ILogger<RenderingViewModel> logger)
    {
        _fractalGenerator = fractalGenerator;
        _zoomService = zoomService;
        _logger = logger;
    }
}
