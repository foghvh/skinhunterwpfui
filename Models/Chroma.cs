using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Windows.Media;

namespace skinhunter.Models
{
    public partial class Chroma : ObservableObject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("chromaPath")]
        public string ChromaPath { get; set; } = string.Empty;

        [JsonPropertyName("colors")]
        public List<string>? Colors { get; set; }

        [JsonIgnore]
        public string ImageUrl => Services.CdragonDataService.GetAssetUrl(ChromaPath);

        [JsonIgnore]
        public System.Windows.Media.Brush? ColorBrush
        {
            get
            {
                if (Colors == null || Colors.Count == 0) return System.Windows.Media.Brushes.Gray;
                if (Colors.Count == 1)
                {
                    try { return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Colors[0])); }
                    catch { return System.Windows.Media.Brushes.Gray; }
                }
                try
                {
                    var gradient = new LinearGradientBrush
                    {
                        StartPoint = new System.Windows.Point(0, 0.5),
                        EndPoint = new System.Windows.Point(1, 0.5)
                    };
                    gradient.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Colors[0]), 0.0));
                    gradient.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Colors[1]), 1.0));
                    return gradient;
                }
                catch { return System.Windows.Media.Brushes.Gray; }
            }
        }

        [ObservableProperty]
        [JsonIgnore]
        private bool _isSelected;
    }
}