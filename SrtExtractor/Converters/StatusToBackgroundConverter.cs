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
            return status switch
            {
                BatchFileStatus.Pending => new SolidColorBrush(Colors.White),
                BatchFileStatus.Processing => new SolidColorBrush(Color.FromRgb(255, 248, 220)), // Light yellow
                BatchFileStatus.Completed => new SolidColorBrush(Color.FromRgb(240, 255, 240)), // Light green
                BatchFileStatus.Error => new SolidColorBrush(Color.FromRgb(255, 240, 240)), // Light red
                BatchFileStatus.Cancelled => new SolidColorBrush(Color.FromRgb(248, 248, 248)), // Light gray
                _ => new SolidColorBrush(Colors.White)
            };
        }
        
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
