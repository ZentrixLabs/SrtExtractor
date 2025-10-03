using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts a double value to Visibility.
/// Returns Visible if the value is greater than 0, Collapsed otherwise.
/// </summary>
public class DoubleToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
