using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SrtExtractor.Models;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts BatchFileStatus to a background brush color.
/// </summary>
public class StatusToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BatchFileStatus status)
        {
            // Use theme-aware colors from DesignTokens.xaml
            return status switch
            {
                BatchFileStatus.Pending => System.Windows.Application.Current.FindResource("BackgroundBrush"),
                BatchFileStatus.Processing => System.Windows.Application.Current.FindResource("WarningBrush"),
                BatchFileStatus.Completed => System.Windows.Application.Current.FindResource("SuccessBrush"),
                BatchFileStatus.Error => System.Windows.Application.Current.FindResource("ErrorBrush"),
                BatchFileStatus.Cancelled => System.Windows.Application.Current.FindResource("BackgroundAltBrush"),
                _ => System.Windows.Application.Current.FindResource("BackgroundBrush")
            };
        }
        
        return System.Windows.Application.Current.FindResource("BackgroundBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
