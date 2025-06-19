using System.Text.Json.Serialization;

namespace skinhunter.Models
{
    public class InstalledSkinInfo
    {
        [JsonPropertyName("champion_id")]
        public int ChampionId { get; set; }

        [JsonPropertyName("skin_id_or_chroma_id")]
        public int SkinOrChromaId { get; set; }

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("folder_name")]
        public string FolderName { get; set; } = string.Empty;

        [JsonPropertyName("skin_name")]
        public string SkinName { get; set; } = string.Empty;

        [JsonPropertyName("chroma_name")]
        public string? ChromaName { get; set; }

        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("installed_at")]
        public DateTime InstalledAt { get; set; }

        public InstalledSkinInfo()
        {
            InstalledAt = DateTime.UtcNow;
        }
    }
}