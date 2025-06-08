using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace skinhunter.Models
{
    public class SupabaseChampionData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;


        [JsonPropertyName("skins")]
        public List<SupabaseSkinData>? Skins { get; set; }
    }

    public class SupabaseSkinData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;


        [JsonPropertyName("chromas")]
        public List<SupabaseChromaData>? Chromas { get; set; }
    }

    public class SupabaseChromaData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("chromaPath")]
        public string ChromaPath { get; set; } = string.Empty;

        [JsonPropertyName("colors")]
        public List<string>? Colors { get; set; }
    }
}