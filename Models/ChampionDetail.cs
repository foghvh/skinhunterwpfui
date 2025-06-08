using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace skinhunter.Models
{
    public class ChampionDetail : ChampionSummary
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("shortBio")]
        public string ShortBio { get; set; } = string.Empty;

        [JsonIgnore]
        public List<Skin> Skins { get; set; } = new List<Skin>();
    }
}