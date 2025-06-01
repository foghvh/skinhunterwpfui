using System;
using System.Globalization;
using System.Windows.Data;

namespace skinhunter.Converters
{
    public class SkinIdToKhadaUrlConverter : IValueConverter
    {
        private const string BaseKhadaUrl = "https://modelviewer.lol/model-viewer?id=";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int skinId && skinId > 0)
            {
                string url = $"{BaseKhadaUrl}{skinId}";
                if (parameter is int chromaId && chromaId > 0 && chromaId / 1000 == skinId)
                {
                    url += $"&chroma={chromaId}";
                }
                return url;
            }
            return BaseKhadaUrl;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}