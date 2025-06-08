using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using skinhunter.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;

namespace skinhunter.Services
{
    public class UserPreferences
    {
        [JsonPropertyName("theme")]
        public string? Theme { get; set; } = "dark";

        [JsonPropertyName("installed_skins_info")]
        public List<InstalledSkinInfo> InstalledSkins { get; set; } = new List<InstalledSkinInfo>();
    }

    public partial class UserPreferencesService : ObservableObject
    {
        private readonly AuthTokenManager _authTokenManager;
        private UserPreferences _currentPreferences = new UserPreferences();
        private bool _isLoaded = false;
        private Guid? _currentUserId = null;
        private readonly string _supabaseUrl = "https://odlqwkgewzxxmbsqutja.supabase.co";
        private readonly string _supabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im9kbHF3a2dld3p4eG1ic3F1dGphIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyMTM2NzcsImV4cCI6MjA0OTc4OTY3N30.qka6a71bavDeUQgy_BKoVavaClRQa_gT36Au7oO9AF0";
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        [ObservableProperty]
        private Profile? _currentProfile;

        public UserPreferencesService(AuthTokenManager authTokenManager)
        {
            _authTokenManager = authTokenManager;
            _authTokenManager.PropertyChanged += async (s, e) => {
                if (e.PropertyName == nameof(AuthTokenManager.IsAuthenticated))
                {
                    if (_authTokenManager.IsAuthenticated)
                    {
                        FileLoggerService.Log("[UserPrefService] Auth state changed to Authenticated. Loading preferences/profile.");
                        await LoadPreferencesAsync();
                    }
                    else
                    {
                        UnloadPreferences();
                    }
                }
            };
        }

        private Guid? GetUserIdFromAuthManager()
        {
            var userIdClaim = _authTokenManager.GetClaim(ClaimTypes.NameIdentifier) ?? _authTokenManager.GetClaim("sub");
            if (Guid.TryParse(userIdClaim, out Guid userId))
            {
                return userId;
            }
            FileLoggerService.Log("[UserPrefService] Could not parse User ID from AuthTokenManager claims.");
            return null;
        }

        private HttpClient GetConfiguredHttpClient()
        {
            var client = new HttpClient();
            if (_authTokenManager.IsAuthenticated && !string.IsNullOrEmpty(_authTokenManager.CurrentToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authTokenManager.CurrentToken);
            }
            client.DefaultRequestHeaders.Add("apikey", _supabaseAnonKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public async Task LoadPreferencesAsync()
        {
            _currentUserId = GetUserIdFromAuthManager();
            if (_currentUserId == null || !_authTokenManager.IsAuthenticated || string.IsNullOrEmpty(_authTokenManager.CurrentToken))
            {
                FileLoggerService.Log("[UserPrefService] Cannot load profile: User ID/Token is null or not authenticated.");
                UnloadPreferences();
                return;
            }

            FileLoggerService.Log($"[UserPrefService] Loading profile for user: {_currentUserId.Value}");
            try
            {
                using var httpClient = GetConfiguredHttpClient();
                string requestUri = $"{_supabaseUrl}/rest/v1/profiles?id=eq.{_currentUserId.Value}&select=*";

                FileLoggerService.Log($"[UserPrefService] Requesting profile from: {requestUri}");
                HttpResponseMessage response = await httpClient.GetAsync(requestUri);
                string jsonResponse = await response.Content.ReadAsStringAsync();
                FileLoggerService.Log($"[UserPrefService] LoadProfile Status: {response.StatusCode}, Raw response: {jsonResponse}");

                if (response.IsSuccessStatusCode)
                {
                    var profilesList = JsonSerializer.Deserialize<List<Profile>>(jsonResponse, _jsonOptions);

                    if (profilesList != null && profilesList.Any())
                    {
                        CurrentProfile = profilesList[0];
                        if (CurrentProfile.Preferences != null)
                        {
                            var prefsJson = JsonSerializer.Serialize(CurrentProfile.Preferences);
                            _currentPreferences = JsonSerializer.Deserialize<UserPreferences>(prefsJson, _jsonOptions) ?? new UserPreferences();
                        }
                        else
                        {
                            _currentPreferences = new UserPreferences();
                        }
                        FileLoggerService.Log($"[UserPrefService] Profile deserialized. Login: {CurrentProfile.Login}, IsBuyer: {CurrentProfile.IsBuyer}. Theme: {_currentPreferences.Theme}, Skins: {_currentPreferences.InstalledSkins.Count}");
                    }
                    else
                    {
                        FileLoggerService.Log("[UserPrefService] Profile not found for authenticated user. Using default and attempting to save a new one.");
                        await CreateAndSaveDefaultProfile();
                    }
                }
                else
                {
                    FileLoggerService.Log($"[UserPrefService] Error loading profile. Status: {response.StatusCode}. Assuming defaults.");
                    UnloadPreferences();
                }
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[UserPrefService] Exception loading profile: {ex.Message}");
                UnloadPreferences();
            }
            _isLoaded = true;
        }

        private async Task CreateAndSaveDefaultProfile()
        {
            var newProfile = new Profile
            {
                Id = _currentUserId!.Value,
                Login = _authTokenManager.GetClaim("email")?.Split('@')[0] ?? "new_user",
                IsBuyer = false,
                Preferences = new Dictionary<string, object?>
                {
                    { "theme", "dark" },
                    { "installed_skins_info", new List<InstalledSkinInfo>() }
                }
            };
            _currentPreferences = new UserPreferences();
            await SaveProfileAsync(newProfile);
            CurrentProfile = newProfile;
        }

        public void UnloadPreferences()
        {
            CurrentProfile = null;
            _currentPreferences = new UserPreferences();
            _isLoaded = false;
            _currentUserId = null;
            FileLoggerService.Log("[UserPrefService] Profile and preferences unloaded.");
        }

        public UserPreferences GetCurrentPreferences()
        {
            if (!_isLoaded && _authTokenManager.IsAuthenticated && _currentUserId != null)
            {
                FileLoggerService.Log("[UserPrefService] Accessing preferences before explicitly loaded, attempting synchronous load.");
                LoadPreferencesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            return _currentPreferences;
        }

        public List<InstalledSkinInfo> GetInstalledSkins()
        {
            return GetCurrentPreferences().InstalledSkins;
        }

        public async Task AddInstalledSkinAsync(InstalledSkinInfo skinInfo)
        {
            if (CurrentProfile == null)
            {
                FileLoggerService.Log("[UserPrefService] Cannot add skin: User profile not loaded.");
                return;
            }
            var prefs = GetCurrentPreferences();
            if (!prefs.InstalledSkins.Any(s => s.SkinOrChromaId == skinInfo.SkinOrChromaId && s.ChampionId == skinInfo.ChampionId))
            {
                prefs.InstalledSkins.Add(skinInfo);
                await SavePreferencesAsync(prefs);
            }
        }

        public async Task RemoveInstalledSkinAsync(int championId, int skinOrChromaId)
        {
            if (CurrentProfile == null)
            {
                FileLoggerService.Log("[UserPrefService] Cannot remove skin: User profile not loaded.");
                return;
            }
            var prefs = GetCurrentPreferences();
            int removedCount = prefs.InstalledSkins.RemoveAll(s => s.ChampionId == championId && s.SkinOrChromaId == skinOrChromaId);
            if (removedCount > 0)
            {
                await SavePreferencesAsync(prefs);
            }
        }

        public async Task ClearAllInstalledSkinsAsync()
        {
            if (CurrentProfile == null)
            {
                FileLoggerService.Log("[UserPrefService] Cannot clear skins: User profile not loaded.");
                return;
            }
            var prefs = GetCurrentPreferences();
            if (prefs.InstalledSkins.Any())
            {
                prefs.InstalledSkins.Clear();
                await SavePreferencesAsync(prefs);
            }
        }

        public async Task SavePreferencesAsync(UserPreferences? preferencesToSave = null)
        {
            var prefsToSave = preferencesToSave ?? _currentPreferences;
            if (CurrentProfile == null)
            {
                FileLoggerService.Log("[UserPrefService] Cannot save preferences: Profile not loaded.");
                return;
            }
            var prefsDict = new Dictionary<string, object?>
            {
                { "theme", prefsToSave.Theme },
                { "installed_skins_info", prefsToSave.InstalledSkins }
            };
            CurrentProfile.Preferences = prefsDict;
            await SaveProfileAsync(CurrentProfile);
        }

        private async Task SaveProfileAsync(Profile profileToSave)
        {
            if (profileToSave == null || _currentUserId == null || !_authTokenManager.IsAuthenticated || string.IsNullOrEmpty(_authTokenManager.CurrentToken))
            {
                FileLoggerService.Log("[UserPrefService] Cannot save profile: User ID/Token/Profile is null or not authenticated.");
                return;
            }

            FileLoggerService.Log($"[UserPrefService] Saving profile for user: {_currentUserId.Value}. Login: {profileToSave.Login}, IsBuyer: {profileToSave.IsBuyer}");
            try
            {
                using var httpClient = GetConfiguredHttpClient();
                string requestUri = $"{_supabaseUrl}/rest/v1/profiles?id=eq.{_currentUserId.Value}";

                string jsonPayload = JsonSerializer.Serialize(profileToSave, _jsonOptions);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                httpClient.DefaultRequestHeaders.Add("Prefer", "return=representation");

                HttpResponseMessage response = await httpClient.PatchAsync(requestUri, content);

                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    FileLoggerService.Log($"[UserPrefService] Profile saved successfully. Response: {responseContent}");
                }
                else
                {
                    FileLoggerService.Log($"[UserPrefService] Failed to save profile. Status: {response.StatusCode}, Content: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[UserPrefService] Error saving profile: {ex.Message} {ex.StackTrace}");
            }
        }
    }
}
