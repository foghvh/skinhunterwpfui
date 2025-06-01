using System;
using System.Globalization;
using System.Windows.Data;
using skinhunter.Models;

namespace skinhunter.Converters
{
    public class ChromaToButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Chroma ? "Download Chroma" : "Download Skin";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}