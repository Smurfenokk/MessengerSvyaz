using System.Globalization;
using System.Windows.Data;

namespace MessengerSvyaz.Converters;

public class FirstCharConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && str.Length > 0)
            return str[0].ToString().ToUpper();
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}