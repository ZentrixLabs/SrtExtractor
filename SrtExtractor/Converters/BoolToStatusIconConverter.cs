using System.Globalization;
using System.Windows.Data;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts boolean values to status icons (checkmark or X) for tool availability display.
/// </summary>
public class BoolToStatusIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isInstalled)
        {
            return isInstalled ? "✅" : "❌";
        }
        
        return "❓";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

