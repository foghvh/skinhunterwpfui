using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using skinhunter.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Net.Http;
using System.Net.Http.Headers;
using System;
using System.Windows;

namespace skinhunter.Services
{
    public class UserPreferences
    {
        [JsonPropertyName("theme")]
        public string? Theme { get; set; } = "dark";

        [JsonPropertyName("installed_skins_info")]
        public List<InstalledSkinInfo> InstalledSkins { get; set; } = [];
    }

    public partial class UserPreferencesService : ObservableObject
    {
        private readonly AuthTokenManager _authTokenManager;
        private UserPreferences _currentPreferences = new();
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
                        await Application.Current.Dispatcher.Invoke(LoadPreferencesAsync);
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(UnloadPreferences);
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
            Application.Current.Dispatcher.VerifyAccess();

            _currentUserId = GetUserIdFromAuthManager();
            if (_currentUserId == null || !_authTokenManager.IsAuthenticated || string.IsNullOrEmpty(_authTokenManager.CurrentToken))
            {
                UnloadPreferences();
                return;
            }

            try
            {
                using var httpClient = GetConfiguredHttpClient();
                string requestUri = $"{_supabaseUrl}/rest/v1/profiles?id=eq.{_currentUserId.Value}&select=*";
                HttpResponseMessage response = await httpClient.GetAsync(requestUri);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var profilesList = JsonSerializer.Deserialize<List<Profile>>(jsonResponse, _jsonOptions);
                    Profile? loadedProfile = profilesList?.FirstOrDefault();

                    if (loadedProfile != null)
                    {
                        CurrentProfile = loadedProfile;
                        if (CurrentProfile.Preferences != null)
                        {
                            var prefsJson = JsonSerializer.Serialize(CurrentProfile.Preferences);
                            _currentPreferences = JsonSerializer.Deserialize<UserPreferences>(prefsJson, _jsonOptions) ?? new();
                        }
                        else
                        {
                            _currentPreferences = new();
                        }
                        _currentPreferences.InstalledSkins ??= [];
                    }
                    else
                    {
                        UnloadPreferences();
                        await CreateAndSaveDefaultProfile();
                    }
                }
                else
                {
                    UnloadPreferences();
                }
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[UserPrefService] Exception loading profile: {ex.Message}");
                UnloadPreferences();
            }
        }

        private async Task CreateAndSaveDefaultProfile()
        {
            var newProfile = new Profile
            {
                Id = _currentUserId!.Value,
                Login = _authTokenManager.GetClaim("email")?.Split('@')[0] ?? "new_user",
                IsBuyer = false,
                Preferences = new()
                {
                    { "theme", "dark" },
                    { "installed_skins_info", new List<InstalledSkinInfo>() }
                }
            };
            _currentPreferences = new UserPreferences { InstalledSkins = [] };
            await SaveProfileAsync(newProfile);
            Application.Current.Dispatcher.Invoke(() => { CurrentProfile = newProfile; });
        }

        public void UnloadPreferences()
        {
            Application.Current.Dispatcher.VerifyAccess();
            CurrentProfile = null;
            _currentPreferences = new UserPreferences { InstalledSkins = [] };
            _currentUserId = null;
        }

        public List<InstalledSkinInfo> GetInstalledSkins()
        {
            return _currentPreferences.InstalledSkins;
        }

        public async Task AddInstalledSkinAsync(InstalledSkinInfo skinInfo)
        {
            if (CurrentProfile == null) return;
            var prefs = _currentPreferences;
            prefs.InstalledSkins.RemoveAll(s => s.ChampionId == skinInfo.ChampionId);
            prefs.InstalledSkins.Add(skinInfo);
            await SavePreferencesAsync(prefs);
        }

        public async Task RemoveInstalledSkinAsync(InstalledSkinInfo skinToRemove)
        {
            if (CurrentProfile == null) return;
            var prefs = _currentPreferences;
            if (prefs.InstalledSkins == null) return;
            int removedCount = prefs.InstalledSkins.RemoveAll(s => s.ChampionId == skinToRemove.ChampionId && s.SkinOrChromaId == skinToRemove.SkinOrChromaId);
            if (removedCount > 0)
            {
                await SavePreferencesAsync(prefs);
            }
        }

        public async Task SavePreferencesAsync(UserPreferences? preferencesToSave = null)
        {
            var prefsToSave = preferencesToSave ?? _currentPreferences;
            if (CurrentProfile == null) return;
            CurrentProfile.Preferences = new()
            {
                { "theme", prefsToSave.Theme },
                { "installed_skins_info", prefsToSave.InstalledSkins ?? [] }
            };
            await SaveProfileAsync(CurrentProfile);
        }

        private async Task SaveProfileAsync(Profile profileToSave)
        {
            if (profileToSave == null || _currentUserId == null || !_authTokenManager.IsAuthenticated || string.IsNullOrEmpty(_authTokenManager.CurrentToken)) return;

            try
            {
                using var httpClient = GetConfiguredHttpClient();
                string requestUri = $"{_supabaseUrl}/rest/v1/profiles?id=eq.{_currentUserId.Value}";
                string jsonPayload = JsonSerializer.Serialize(profileToSave, _jsonOptions);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                httpClient.DefaultRequestHeaders.Add("Prefer", "return=representation,resolution=merge-duplicates");
                HttpResponseMessage response = await httpClient.PatchAsync(requestUri, content);
                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
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