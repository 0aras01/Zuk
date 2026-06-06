using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace Fractal.UI.Services;

public class LocalizationService : INotifyPropertyChanged
{
    private static readonly LocalizationService _instance = new();
    public static LocalizationService Instance => _instance;

    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService()
    {
        _resourceManager = new ResourceManager("Fractal.UI.Resources.Resources", typeof(LocalizationService).Assembly);
        
        // Default to system culture if it is PL, otherwise English
        var sysCulture = CultureInfo.CurrentCulture;
        if (sysCulture.Name.StartsWith("pl", StringComparison.OrdinalIgnoreCase))
        {
            _currentCulture = new CultureInfo("pl");
        }
        else
        {
            _currentCulture = new CultureInfo("en");
        }
    }

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (value != null && !_currentCulture.Equals(value))
            {
                _currentCulture = value;
                CultureInfo.CurrentCulture = value;
                CultureInfo.CurrentUICulture = value;
                OnPropertyChanged(string.Empty); // Notifies that all indexer values/properties have changed
            }
        }
    }

    public string this[string key]
    {
        get
        {
            try
            {
                var val = _resourceManager.GetString(key, _currentCulture);
                return val ?? key;
            }
            catch
            {
                return key;
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
