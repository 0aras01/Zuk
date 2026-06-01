using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Mandelbrot.UI.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Mandelbrot.UI.WinUI;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        this.InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        ViewModel = ((App)Application.Current).Services!.GetRequiredService<MainViewModel>();
    }

    private void ViewportCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(ViewportCanvas).Properties;
        if (properties.IsLeftButtonPressed)
        {
            var point = e.GetCurrentPoint(ViewportCanvas).Position;
            ViewModel.OnPointerPressed(point.X, point.Y);
        }
        else if (properties.IsRightButtonPressed)
        {
            ViewModel.ZoomOutCommand.Execute(null);
        }
    }

    private void ViewportCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(ViewportCanvas).Position;
        ViewModel.OnPointerMoved(point.X, point.Y);
    }

    private void ViewportCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(ViewportCanvas).Position;
        ViewModel.OnPointerReleased(point.X, point.Y);
    }

    private void ViewportCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            ViewModel.OnSizeChanged((int)e.NewSize.Width, (int)e.NewSize.Height);
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeChoices.Add("PNG Image", new[] { ".png" });
        picker.SuggestedFileName = "Mandelbrot_4K";

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await ViewModel.ExportToFileAsync(file.Path);
        }
    }
}
