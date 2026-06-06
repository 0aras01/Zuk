using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Fractal.Core.Models;
using Fractal.UI.ViewModels;

namespace Fractal.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Wire up ViewModel delegates once the DataContext is set
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Save-file dialog delegate
            vm.SaveFileDialogAction = async () =>
            {
                var storageProvider = StorageProvider;
                var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Fractal Image",
                    SuggestedFileName = $"Fractal_{DateTime.Now:yyyyMMdd_HHmmss}",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                        new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                        new FilePickerFileType("BMP Image") { Patterns = new[] { "*.bmp" } }
                    },
                    DefaultExtension = "png"
                });

                return file?.Path.LocalPath;
            };

            // Clipboard copy delegate
            vm.CopyToClipboardAction = async () =>
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null && vm.Rendering.FractalImage != null)
                {
                    await clipboard.SetBitmapAsync(vm.Rendering.FractalImage);
                }
            };

            // Fullscreen toggle delegate
            vm.ToggleFullscreenAction = () =>
            {
                WindowState = WindowState == WindowState.FullScreen
                    ? WindowState.Normal
                    : WindowState.FullScreen;
            };
        }
    }

    private void ViewportCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var point = e.GetCurrentPoint(sender as Control);
            if (point.Properties.IsLeftButtonPressed)
            {
                vm.Navigation.OnPointerPressed(point.Position);
            }
            else if (point.Properties.IsMiddleButtonPressed)
            {
                vm.Navigation.StartPan(point.Position);
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                vm.Navigation.ZoomOutCommand.Execute(null);
            }
        }
    }

    private void ViewportCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var point = e.GetCurrentPoint(sender as Control);
            if (vm.Navigation.IsPanning)
            {
                vm.Navigation.MovePan(point.Position);
            }
            else if (vm.Navigation.IsSelecting)
            {
                vm.Navigation.OnPointerMoved(point.Position);
            }
            else
            {
                // Update cursor coordinates when not panning or selecting
                vm.Navigation.UpdateCursorCoordinates(point.Position);
            }
        }
    }

    private void ViewportCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var point = e.GetCurrentPoint(sender as Control);
            if (vm.Navigation.IsPanning)
            {
                vm.Navigation.EndPan();
            }
            else
            {
                vm.Navigation.OnPointerReleased(point.Position);
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
                vm.Navigation.OnSizeChanged(w, h);
            }
        }
    }

    private void ViewportCanvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var point = e.GetCurrentPoint(sender as Control);
            double delta = e.Delta.Y;
            if (delta != 0)
            {
                vm.Navigation.OnMouseWheelZoom(point.Position, delta);
                e.Handled = true;
            }
        }
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Don't handle keyboard shortcuts when a TextBox has focus
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
            return;

        switch (e.Key)
        {
            // Arrow keys: pan 10% of view
            case Key.Left:
                vm.Navigation.PanByPercent(-0.1, 0);
                e.Handled = true;
                break;
            case Key.Right:
                vm.Navigation.PanByPercent(0.1, 0);
                e.Handled = true;
                break;
            case Key.Up:
                vm.Navigation.PanByPercent(0, 0.1); // positive Y = move up in imaginary axis
                e.Handled = true;
                break;
            case Key.Down:
                vm.Navigation.PanByPercent(0, -0.1);
                e.Handled = true;
                break;

            // +/= key: zoom in 2x centered
            case Key.OemPlus:
            case Key.Add:
                vm.Navigation.ZoomCentered(zoomIn: true);
                e.Handled = true;
                break;

            // - key: zoom out 2x centered
            case Key.OemMinus:
            case Key.Subtract:
                vm.Navigation.ZoomCentered(zoomIn: false);
                e.Handled = true;
                break;

            // R key: reset view
            case Key.R:
                vm.Navigation.ResetCommand.Execute(null);
                e.Handled = true;
                break;

            // D key: toggle diagnostics panel
            case Key.D:
                vm.Diagnostics.IsDiagnosticsVisible = !vm.Diagnostics.IsDiagnosticsVisible;
                e.Handled = true;
                break;

            // F11: toggle fullscreen
            case Key.F11:
                vm.ToggleFullscreenAction?.Invoke();
                e.Handled = true;
                break;

            // Tab key: toggle side panel
            case Key.Tab:
                vm.ToggleSidePanelCommand.Execute(null);
                e.Handled = true;
                break;

            // Escape: cancel selection or exit fullscreen
            case Key.Escape:
                if (vm.Navigation.IsSelecting)
                {
                    vm.Navigation.CancelSelection();
                }
                else if (WindowState == WindowState.FullScreen)
                {
                    WindowState = WindowState.Normal;
                }
                e.Handled = true;
                break;

            // 1-4: quick palette selection
            case Key.D1:
                if (vm.Rendering.Palettes.Count > 0) vm.Rendering.SelectedPalette = vm.Rendering.Palettes[0];
                e.Handled = true;
                break;
            case Key.D2:
                if (vm.Rendering.Palettes.Count > 1) vm.Rendering.SelectedPalette = vm.Rendering.Palettes[1];
                e.Handled = true;
                break;
            case Key.D3:
                if (vm.Rendering.Palettes.Count > 2) vm.Rendering.SelectedPalette = vm.Rendering.Palettes[2];
                e.Handled = true;
                break;
            case Key.D4:
                if (vm.Rendering.Palettes.Count > 3) vm.Rendering.SelectedPalette = vm.Rendering.Palettes[3];
                e.Handled = true;
                break;

            // Ctrl+C: copy image to clipboard
            case Key.C when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.Rendering.CopyToClipboardCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+S: save image
            case Key.S when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.Rendering.SaveImageCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
