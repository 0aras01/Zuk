using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fractal.Core.Services;
using Fractal.Compute;
using Fractal.UI.ViewModels;
using Fractal.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace Fractal.UI;

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

        // Add logging configuration
        collection.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Try GPU acceleration first, fall back to CPU if unavailable
        collection.AddSingleton<IFractalGenerator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<App>>();
            try
            {
                var gpu = new ILGPUFractalGenerator();
                logger.LogInformation("GPU acceleration initialized: {GpuName}", gpu.Name);
                return gpu;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "GPU initialization failed ({Message}), falling back to CPU.", ex.Message);
                return new ParallelFractalGenerator();
            }
        });

        collection.AddSingleton<IZoomService, ZoomService>();
        collection.AddSingleton<BookmarkService>();
        collection.AddTransient<NavigationViewModel>();
        collection.AddTransient<DiagnosticsViewModel>();
        collection.AddTransient<RenderingViewModel>();
        collection.AddTransient<MainViewModel>();

        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += (_, _) =>
            {
                if (Services is IDisposable disposable)
                    disposable.Dispose();
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}


