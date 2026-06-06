using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Fractal.UI.Views;

public partial class PaletteEditorWindow : Window
{
    public PaletteEditorWindow()
    {
        InitializeComponent();
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
