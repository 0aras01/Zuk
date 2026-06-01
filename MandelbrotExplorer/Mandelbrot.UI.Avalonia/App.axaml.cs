using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mandelbrot.Core.Services;
using Mandelbrot.Core.Export;
using Mandelbrot.Compute;
using Mandelbrot.UI.Avalonia.ViewModels;
using Mandelbrot.UI.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Mandelbrot.UI.Avalonia;

public partial class App : Application
{
    public new static App? Current => Application.Current as App;

    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Dependency Injection Setup
        var collection = new ServiceCollection();
        collection.AddSingleton<IFractalGenerator, ILGPUFractalGenerator>();
        collection.AddSingleton<IZoomService, ZoomService>();
        collection.AddSingleton<IFileExportService, Mandelbrot.Core.Export.SkiaExportService>();
        collection.AddSingleton<IBookmarkService, BookmarkService>();
        collection.AddTransient<MainViewModel>();

        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
