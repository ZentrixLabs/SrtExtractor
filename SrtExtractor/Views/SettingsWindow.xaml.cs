using System.Windows;
using SrtExtractor.ViewModels;
using SrtExtractor.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace SrtExtractor.Views;

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IServiceProvider _serviceProvider;

    public SettingsWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        DataContext = viewModel;
        _serviceProvider = serviceProvider;
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

    private void OpenSupOcrTool_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Create the SUP OCR tool window the same way as MainWindow does
            var supOcrViewModel = new SupOcrViewModel(
                _serviceProvider.GetRequiredService<ISubtitleOcrService>(),
                _serviceProvider.GetRequiredService<ILoggingService>(),
                _serviceProvider.GetRequiredService<INotificationService>()
            );

            var supOcrWindow = new SupOcrWindow
            {
                DataContext = supOcrViewModel,
                Owner = this
            };
            
            supOcrWindow.Show();
            
            // Log the action
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.State.AddLogMessage("SUP OCR Tool opened from settings");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open SUP OCR Tool:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
