using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MessengerSvyaz.Converters;

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter as string == "Inverse";
        var isEmpty = string.IsNullOrWhiteSpace(value as string);
        
        if (invert)
            return isEmpty ? Visibility.Visible : Visibility.Collapsed;
        else
            return isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}