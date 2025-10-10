using System.Globalization;
using System.Windows.Data;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts boolean values to their inverse (true = false, false = true).
/// Used for IsEnabled bindings that need negation.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        
        return false;
    }
}

