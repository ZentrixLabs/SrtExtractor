using System.Windows;
using SrtExtractor.ViewModels;

namespace SrtExtractor.Views;

/// <summary>
/// Welcome/Onboarding window for first-run experience.
/// </summary>
public partial class WelcomeWindow : Window
{
    public WelcomeWindow(WelcomeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Handle window closing via ViewModel
        viewModel.CloseRequested += () => DialogResult = true;
    }
}

