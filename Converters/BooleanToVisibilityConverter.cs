using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace skinhunter.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = false;
            if (value is bool b)
            {
                flag = b;
            }
            else if (value is bool?)
            {
                bool? nullable = (bool?)value;
                flag = nullable.HasValue && nullable.Value;
            }

            bool inverse = (parameter as string) == "Inverse";
            if (inverse)
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverse = (parameter as string) == "Inverse";
            bool flag = (value is Visibility visibility) && visibility == Visibility.Visible;

            if (inverse)
            {
                flag = !flag;
            }
            return flag;
        }
    }
}