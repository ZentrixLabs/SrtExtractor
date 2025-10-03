using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SrtExtractor.Views;

/// <summary>
/// Interaction logic for AboutView.xaml
/// </summary>
public partial class AboutView : UserControl
{
    public string VersionText { get; }

    public AboutView()
    {
        InitializeComponent();
        
        // Get version from assembly
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText = $"Version: {version?.ToString(3) ?? "Unknown"}";
        
        // Set DataContext to enable binding
        DataContext = this;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // Open the URL in the default browser
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
