using System.Windows;
using SrtExtractor.ViewModels;

namespace SrtExtractor.Views;

public partial class SupOcrWindow : Window
{
    public SupOcrWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

