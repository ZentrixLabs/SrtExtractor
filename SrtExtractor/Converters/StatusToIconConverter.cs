using System;
using System.Globalization;
using System.Windows.Data;
using SrtExtractor.Models;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts BatchFileStatus to an icon emoji.
/// </summary>
public class StatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BatchFileStatus status)
        {
            return status switch
            {
                BatchFileStatus.Pending => "⏳",
                BatchFileStatus.Processing => "🔄",
                BatchFileStatus.Completed => "✅",
                BatchFileStatus.Error => "❌",
                BatchFileStatus.Cancelled => "⏹️",
                _ => "❓"
            };
        }
        
        return "❓";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
