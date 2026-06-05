using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Fractal.UI.ViewModels;

public class DiagnosticsViewModel : ObservableObject
{
    private readonly ILogger<DiagnosticsViewModel>? _logger;

    public DiagnosticsViewModel()
    {
    }

    public DiagnosticsViewModel(ILogger<DiagnosticsViewModel> logger)
    {
        _logger = logger;
    }
}
