using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Mandelbrot.UI.Avalonia.ViewModels;
using Avalonia.Platform.Storage;

namespace Mandelbrot.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ViewportCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var point = e.GetCurrentPoint(sender as Control);
            if (point.Properties.IsLeftButtonPressed)
            {
                vm.OnPointerPressed(point.Position);
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                vm.ZoomOutCommand.Execute(null);
            }
        }
    }

    private void ViewportCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var point = e.GetCurrentPoint(sender as Control);
            vm.OnPointerMoved(point.Position);
        }
    }

    private void ViewportCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var point = e.GetCurrentPoint(sender as Control);
            vm.OnPointerReleased(point.Position);
        }
    }

    private void TopLevel_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Accounting for the 300px sidebar and the 50px bottom bar
            int w = (int)(e.NewSize.Width - 300);
            int h = (int)(e.NewSize.Height - 50);
            if (w > 0 && h > 0)
            {
                vm.OnSizeChanged(w, h);
            }
        }
    }

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Fractal Image",
                DefaultExtension = "png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } }
                }
            });

            if (file != null)
            {
                await vm.ExportToFileAsync(file.Path.LocalPath);
            }
        }
    }
}
