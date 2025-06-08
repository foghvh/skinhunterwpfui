using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace skinhunter.Models
{
    public class Profile
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("login")]
        public string? Login { get; set; }

        [JsonPropertyName("is_buyer")]
        public bool IsBuyer { get; set; }

        [JsonPropertyName("avatar_id")]
        public string? AvatarId { get; set; }

        [JsonPropertyName("preferences")]
        public Dictionary<string, object?>? Preferences { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
