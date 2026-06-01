using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Mandelbrot.Core.Services;
using Mandelbrot.Core.Export;
using Mandelbrot.Compute;
using Mandelbrot.UI.ViewModels;
using System;

namespace Mandelbrot.UI.WinUI;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }
    private Window? m_window;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IFractalGenerator, ILGPUFractalGenerator>();
        collection.AddSingleton<IZoomService, ZoomService>();
        collection.AddSingleton<IFileExportService, Mandelbrot.Core.Export.SkiaExportService>();
        collection.AddSingleton<IBookmarkService, BookmarkService>();
        collection.AddTransient<MainViewModel>();

        Services = collection.BuildServiceProvider();

        m_window = new MainWindow
        {
            ExtendsContentIntoTitleBar = true
        };
        m_window.Activate();
    }
}
