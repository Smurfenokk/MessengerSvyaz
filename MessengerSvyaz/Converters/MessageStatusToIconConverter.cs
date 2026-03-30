using System;
using System.Globalization;
using System.Windows.Data;
using MessengerSvyaz.Models;

namespace MessengerSvyaz.Converters;

public class MessageStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MessageStatus status)
        {
            return status switch
            {
                MessageStatus.Sending => "🕒",
                MessageStatus.Sent => "✓",
                MessageStatus.Delivered => "✓✓",
                MessageStatus.Read => "✓✓",
                _ => ""
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
