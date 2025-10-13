using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts an enum value to Visibility based on whether it matches the converter parameter.
/// Used for showing/hiding UI elements based on enum values.
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var enumValue = value.ToString();
        var parameterValue = parameter.ToString();
        
        return enumValue.Equals(parameterValue, StringComparison.OrdinalIgnoreCase) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("EnumToVisibilityConverter does not support ConvertBack");
    }
}
