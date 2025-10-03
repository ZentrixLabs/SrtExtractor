using System.Windows;
using SrtExtractor.ViewModels;

namespace SrtExtractor.Views;

/// <summary>
/// Interaction logic for BatchSrtCorrectionWindow.xaml
/// </summary>
public partial class BatchSrtCorrectionWindow : Window
{
    public BatchSrtCorrectionWindow(BatchSrtCorrectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
