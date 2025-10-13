using System;
using System.Globalization;
using System.Windows.Data;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts an enum value to a boolean based on whether it matches the converter parameter.
/// Used for radio button binding with enum values.
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        var enumValue = value.ToString();
        var parameterValue = parameter.ToString();
        
        return enumValue.Equals(parameterValue, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, parameter.ToString()!, true);
            }
        }
        
        return Binding.DoNothing;
    }
}
