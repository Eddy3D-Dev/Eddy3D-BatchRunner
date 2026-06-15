using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BatchRunner.Models;

namespace BatchRunner.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is JobStatus status)
        {
            return status switch
            {
                JobStatus.Queued => new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5A)), // Gray
                JobStatus.Running => new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)), // Blue
                JobStatus.Completed => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), // Green
                JobStatus.Failed => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)), // Red
                JobStatus.Cancelled => new SolidColorBrush(Color.FromRgb(0xC6, 0x3A, 0x12)), // Orange
                _ => Brushes.Black
            };
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
