/// skinhunter Start of Converters/SymbolToIconConverter.cs ///
using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace skinhunter.Converters
{
    public class SymbolToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SymbolRegular symbol)
            {
                return new SymbolIcon(symbol);
            }
            return new SymbolIcon(SymbolRegular.ErrorCircle24);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
/// skinhunter End of Converters/SymbolToIconConverter.cs ///