using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Fractal.UI.ViewModels;

namespace Fractal.UI.Views;

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
            else if (point.Properties.IsMiddleButtonPressed)
            {
                vm.StartPan(point.Position);
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
            if (vm.IsPanning)
            {
                vm.MovePan(point.Position);
            }
            else
            {
                vm.OnPointerMoved(point.Position);
            }
        }
    }

    private void ViewportCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var point = e.GetCurrentPoint(sender as Control);
            if (vm.IsPanning)
            {
                vm.EndPan();
            }
            else
            {
                vm.OnPointerReleased(point.Position);
            }
        }
    }

    private void ViewportCanvas_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            int w = (int)e.NewSize.Width;
            int h = (int)e.NewSize.Height;
            if (w > 0 && h > 0)
            {
                vm.OnSizeChanged(w, h);
            }
        }
    }
}

