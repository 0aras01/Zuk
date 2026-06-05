using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mandelbrot.Core.Services;
using Mandelbrot.Compute;
using Mandelbrot.UI.ViewModels;
using Mandelbrot.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Mandelbrot.UI;

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

        // Try GPU acceleration first, fall back to CPU if unavailable
        collection.AddSingleton<IFractalGenerator>(sp =>
        {
            try
            {
                var gpu = new ILGPUFractalGenerator();
                Console.WriteLine($"[Mandelbrot] GPU acceleration initialized: {gpu.Name}");
                return gpu;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Mandelbrot] GPU initialization failed ({ex.Message}), falling back to CPU.");
                return new ParallelFractalGenerator();
            }
        });

        collection.AddSingleton<IZoomService, ZoomService>();
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

