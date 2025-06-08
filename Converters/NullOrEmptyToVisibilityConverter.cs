using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace skinhunter.Converters
{
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNullOrEmpty;

            if (value == null)
            {
                isNullOrEmpty = true;
            }
            else if (value is string s)
            {
                isNullOrEmpty = string.IsNullOrEmpty(s);
            }
            else if (value is ICollection c)
            {
                isNullOrEmpty = c.Count == 0;
            }
            else
            {
                isNullOrEmpty = false;
            }

            bool collapseWhenNullOrEmpty = true;
            if (parameter is string paramString && bool.TryParse(paramString, out bool paramBool))
            {
                collapseWhenNullOrEmpty = paramBool;
            }
            else if (parameter is bool directBool)
            {
                collapseWhenNullOrEmpty = directBool;
            }


            if (collapseWhenNullOrEmpty)
            {
                return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}