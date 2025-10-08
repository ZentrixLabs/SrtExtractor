using System.Windows;
using SrtExtractor.ViewModels;

namespace SrtExtractor.Views;

/// <summary>
/// Interaction logic for VobSubTrackAnalyzerWindow.xaml
/// </summary>
public partial class VobSubTrackAnalyzerWindow : Window
{
    public VobSubTrackAnalyzerWindow(VobSubTrackAnalyzerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
