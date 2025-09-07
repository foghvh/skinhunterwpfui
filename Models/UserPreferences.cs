using System.Text.Json.Serialization;
using System.Collections.Generic;
using skinhunter.Models;

namespace skinhunter.Services
{
    public class UserPreferences
    {
        [JsonPropertyName("theme")]
        public string? Theme { get; set; } = "dark";

        [JsonPropertyName("sync_on_start")]
        public bool SyncOnStart { get; set; } = true;

        [JsonPropertyName("installed_skins_info")]
        public List<InstalledSkinInfo> InstalledSkins { get; set; } = [];

        [JsonPropertyName("game_path")]
        public string? GamePath { get; set; }

        [JsonPropertyName("backdrop_type")]
        public string? BackdropType { get; set; } = "Mica";
    }
}