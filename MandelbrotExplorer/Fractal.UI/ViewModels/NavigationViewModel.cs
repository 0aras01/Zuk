using CommunityToolkit.Mvvm.ComponentModel;
using Fractal.Core.Services;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public class NavigationViewModel : ObservableObject
{
    private readonly IZoomService? _zoomService;
    private readonly BookmarkService? _bookmarkService;
    private readonly ILogger<NavigationViewModel>? _logger;

    public NavigationViewModel()
    {
    }

    public NavigationViewModel(IZoomService zoomService, BookmarkService bookmarkService, ILogger<NavigationViewModel> logger)
    {
        _zoomService = zoomService;
        _bookmarkService = bookmarkService;
        _logger = logger;
    }
}
