/// skinhunter Start of Models\InstalledSkinInfo.cs ///
using System.Text.Json.Serialization;

namespace skinhunter.Models
{
    public class InstalledSkinInfo
    {
        [JsonPropertyName("champion_id")]
        public int ChampionId { get; set; }

        [JsonPropertyName("skin_id_or_chroma_id")] // Representa el ID del asset específico descargado (skin base o chroma)
        public int SkinOrChromaId { get; set; }

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("skin_name")] // Nombre base de la skin (sin chroma)
        public string SkinName { get; set; } = string.Empty;

        [JsonPropertyName("chroma_name")] // Nombre del chroma si aplica, sino null o vacío
        public string? ChromaName { get; set; }

        [JsonPropertyName("image_url")] // URL del tile de la skin/chroma
        public string ImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("installed_at")]
        public DateTime InstalledAt { get; set; }

        // Constructor para facilitar la creación
        public InstalledSkinInfo()
        {
            InstalledAt = DateTime.UtcNow;
        }
    }
}
/// skinhunter End of Models\InstalledSkinInfo.cs ///