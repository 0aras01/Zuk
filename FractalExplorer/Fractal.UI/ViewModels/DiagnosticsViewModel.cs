using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly ILogger<DiagnosticsViewModel>? _logger;

    public MainViewModel Main { get; internal set; } = null!;

    [ObservableProperty]
    private bool _isDiagnosticsVisible = true;

    [ObservableProperty]
    private string _zoomText = "";

    [ObservableProperty]
    private string _engineText = "";

    [ObservableProperty]
    private string _iterationsText = "";

    [ObservableProperty]
    private string _renderTimeText = "";

    [ObservableProperty]
    private string _resolutionText = "";

    [ObservableProperty]
    private string _spanText = "";

    [ObservableProperty]
    private string _centerCoordinatesText = "";

    public DiagnosticsViewModel()
    {
    }

    public DiagnosticsViewModel(ILogger<DiagnosticsViewModel> logger)
    {
        _logger = logger;
    }
}
