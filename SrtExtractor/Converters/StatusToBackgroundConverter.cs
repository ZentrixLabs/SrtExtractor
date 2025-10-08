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
                BatchFileStatus.Pending => new SolidColorBrush(Color.FromRgb(255, 255, 255)),      // #FFFFFF (StatusPendingColor)
                BatchFileStatus.Processing => new SolidColorBrush(Color.FromRgb(255, 248, 220)),  // #FFF8DC (StatusProcessingColor)
                BatchFileStatus.Completed => new SolidColorBrush(Color.FromRgb(240, 255, 240)),   // #F0FFF0 (StatusCompletedColor)
                BatchFileStatus.Error => new SolidColorBrush(Color.FromRgb(255, 240, 240)),       // #FFF0F0 (StatusErrorColor)
                BatchFileStatus.Cancelled => new SolidColorBrush(Color.FromRgb(248, 248, 248)),   // #F8F8F8 (StatusCancelledColor)
                _ => new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };
        }
        
        return new SolidColorBrush(Color.FromRgb(255, 255, 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
