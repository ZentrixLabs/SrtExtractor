using System;
using System.Globalization;
using System.Windows.Data;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts duration in seconds to a human-readable format (HH:MM:SS or MM:SS).
/// </summary>
public class DurationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double seconds)
        {
            return FormatDuration(seconds);
        }
        
        if (value is null)
        {
            return "-";
        }
        
        return value.ToString() ?? "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Format duration in seconds to HH:MM:SS or MM:SS format.
    /// </summary>
    /// <param name="totalSeconds">Total duration in seconds</param>
    /// <returns>Formatted duration string</returns>
    private static string FormatDuration(double totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return "0:00";
        }

        var timeSpan = TimeSpan.FromSeconds(totalSeconds);
        
        // If less than an hour, show MM:SS
        if (timeSpan.TotalHours < 1)
        {
            return $"{(int)timeSpan.TotalMinutes:D2}:{timeSpan.Seconds:D2}";
        }
        
        // If an hour or more, show HH:MM:SS
        return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }
}
