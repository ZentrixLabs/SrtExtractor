using System.Windows;
using SrtExtractor.ViewModels;

namespace SrtExtractor.Views;

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Save settings when user clicks OK
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.SaveSettingsFromDialogAsync();
        }
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.State.ClearLog();
            MessageBox.Show("Application log cleared.", "Log Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
