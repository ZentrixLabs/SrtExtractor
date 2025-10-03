using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace SrtExtractor.Converters;

/// <summary>
/// Converter to extract filename from full file path for display in menus.
/// </summary>
public class FileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string filePath && !string.IsNullOrWhiteSpace(filePath))
        {
            return Path.GetFileName(filePath);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
