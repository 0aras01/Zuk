using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fractal.Core.Models;
using Fractal.Core.Services;

namespace Fractal.UI.ViewModels;

public partial class PaletteEditorViewModel : ObservableObject
{
    private readonly IPaletteService _paletteService;
    private readonly RenderingViewModel _renderingViewModel;
    private bool _isInitializing;

    [ObservableProperty]
    private string _paletteName = "New Palette";

    public ObservableCollection<GradientStopViewModel> Stops { get; } = new();

    [ObservableProperty]
    private GradientStopViewModel? _selectedStop;

    public PaletteEditorViewModel(IPaletteService paletteService, RenderingViewModel renderingViewModel, GradientPalette? initialPalette = null)
    {
        _isInitializing = true;
        _paletteService = paletteService;
        _renderingViewModel = renderingViewModel;

        if (initialPalette != null)
        {
            PaletteName = initialPalette.Name;
            foreach (var stop in initialPalette.Stops)
            {
                Stops.Add(new GradientStopViewModel { Position = stop.Position, R = stop.R, G = stop.G, B = stop.B });
            }
        }

        Stops.CollectionChanged += (s, e) => {
            UpdatePreview();
            if (e.NewItems != null) {
                foreach(GradientStopViewModel item in e.NewItems) {
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }
            if (e.OldItems != null) {
                foreach(GradientStopViewModel item in e.OldItems) {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }
        };

        foreach(var stop in Stops) {
            stop.PropertyChanged += Item_PropertyChanged;
        }

        _isInitializing = false;
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        OnPropertyChanged(nameof(GradientPreview));

        if (!_isInitializing)
        {
            var tempPalette = new GradientPalette { Name = PaletteName, IsBuiltIn = false };
            foreach (var s in Stops.OrderBy(x => x.Position))
            {
                tempPalette.Stops.Add(new GradientStop(s.Position, s.R, s.G, s.B));
            }
            _renderingViewModel.SelectedPalette = tempPalette;
        }
    }

    public Avalonia.Media.IBrush GradientPreview
    {
        get
        {
            var brush = new Avalonia.Media.LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 0, Avalonia.RelativeUnit.Relative)
            };
            foreach (var s in Stops.OrderBy(x => x.Position))
            {
                brush.GradientStops.Add(new Avalonia.Media.GradientStop(Avalonia.Media.Color.FromRgb(s.R, s.G, s.B), s.Position));
            }
            return brush;
        }
    }

    [RelayCommand]
    private void AddStop()
    {
        Stops.Add(new GradientStopViewModel { Position = 1.0, R = 255, G = 255, B = 255 });
    }

    [RelayCommand]
    private void RemoveStop()
    {
        if (SelectedStop != null)
        {
            Stops.Remove(SelectedStop);
        }
    }

    [RelayCommand]
    private void Save()
    {
        var newPalette = new GradientPalette { Name = PaletteName, IsBuiltIn = false };
        foreach (var s in Stops.OrderBy(x => x.Position))
        {
            newPalette.Stops.Add(new GradientStop(s.Position, s.R, s.G, s.B));
        }

        _renderingViewModel.Palettes.Add(newPalette);
        _renderingViewModel.SelectedPalette = newPalette;

        var allPalettes = new System.Collections.Generic.List<GradientPalette>(_renderingViewModel.Palettes);
        _paletteService.SavePalettes(allPalettes);
    }
}

public partial class GradientStopViewModel : ObservableObject
{
    [ObservableProperty]
    private double _position;

    [ObservableProperty]
    private byte _r;

    [ObservableProperty]
    private byte _g;

    [ObservableProperty]
    private byte _b;
}
