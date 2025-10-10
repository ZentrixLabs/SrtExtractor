using System.Windows;

namespace SrtExtractor.Views;

/// <summary>
/// Interaction logic for KeyboardShortcutsWindow.xaml
/// </summary>
public partial class KeyboardShortcutsWindow : Window
{
    public KeyboardShortcutsWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

