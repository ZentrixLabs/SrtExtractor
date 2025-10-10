using System.Globalization;
using System.Windows.Data;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts boolean values to preference text for subtitle type display.
/// </summary>
public class BoolToPreferenceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool preferForced)
        {
            return preferForced ? "forced" : "normal";
        }
        
        return "normal";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

