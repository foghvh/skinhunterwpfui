using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace skinhunter.Converters
{
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool collapseWhenNull = true;

            if (parameter is string paramString && bool.TryParse(paramString, out bool paramBool))
            {
                collapseWhenNull = paramBool;
            }

            if (collapseWhenNull)
            {
                return isNull ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}