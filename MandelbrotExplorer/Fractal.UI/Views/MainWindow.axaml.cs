using System;
using System.Collections.Generic;
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
                if (clipboard != null && vm.FractalImage != null)
                {
                    await clipboard.SetBitmapAsync(vm.FractalImage);
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
            else if (vm.IsSelecting)
            {
                vm.OnPointerMoved(point.Position);
            }
            else
            {
                // Update cursor coordinates when not panning or selecting
                vm.UpdateCursorCoordinates(point.Position);
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

    private void ViewportCanvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var point = e.GetCurrentPoint(sender as Control);
            double delta = e.Delta.Y;
            if (delta != 0)
            {
                vm.OnMouseWheelZoom(point.Position, delta);
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
                vm.PanByPercent(-0.1, 0);
                e.Handled = true;
                break;
            case Key.Right:
                vm.PanByPercent(0.1, 0);
                e.Handled = true;
                break;
            case Key.Up:
                vm.PanByPercent(0, 0.1); // positive Y = move up in imaginary axis
                e.Handled = true;
                break;
            case Key.Down:
                vm.PanByPercent(0, -0.1);
                e.Handled = true;
                break;

            // +/= key: zoom in 2x centered
            case Key.OemPlus:
            case Key.Add:
                vm.ZoomCentered(zoomIn: true);
                e.Handled = true;
                break;

            // - key: zoom out 2x centered
            case Key.OemMinus:
            case Key.Subtract:
                vm.ZoomCentered(zoomIn: false);
                e.Handled = true;
                break;

            // R key: reset view
            case Key.R:
                vm.ResetCommand.Execute(null);
                e.Handled = true;
                break;

            // D key: toggle diagnostics panel
            case Key.D:
                vm.IsDiagnosticsVisible = !vm.IsDiagnosticsVisible;
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
                if (vm.IsSelecting)
                {
                    vm.CancelSelection();
                }
                else if (WindowState == WindowState.FullScreen)
                {
                    WindowState = WindowState.Normal;
                }
                e.Handled = true;
                break;

            // 1-4: quick palette selection
            case Key.D1:
                vm.SelectedPalette = PaletteType.Sunset;
                e.Handled = true;
                break;
            case Key.D2:
                vm.SelectedPalette = PaletteType.Ice;
                e.Handled = true;
                break;
            case Key.D3:
                vm.SelectedPalette = PaletteType.Rainbow;
                e.Handled = true;
                break;
            case Key.D4:
                vm.SelectedPalette = PaletteType.Forest;
                e.Handled = true;
                break;

            // Ctrl+C: copy image to clipboard
            case Key.C when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.CopyToClipboardCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+S: save image
            case Key.S when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.SaveImageCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
