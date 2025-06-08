using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using skinhunter.Models;
using System.Linq;
using System.IO;
using System.Net.Http.Headers;
using System.Diagnostics;

namespace skinhunter.Services
{
    public static class CdragonDataService
    {
        private static readonly HttpClient _httpClient = CreateHttpClient();
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        private const string CdragonBaseUrl = "https://raw.communitydragon.org/latest";
        private const string DataRoot = $"{CdragonBaseUrl}/plugins/rcp-be-lol-game-data/global/default";
        private static string? _cdragonVersion;

        private const string SupabaseUrl = "https://odlqwkgewzxxmbsqutja.supabase.co";
        private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im9kbHF3a2dld3p4eG1ic3F1dGphIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyMTM2NzcsImV4cCI6MjA0OTc4OTY3N30.qka6a71bavDeUQgy_BKoVavaClRQa_gT36Au7oO9AF0";
        private const string SupabaseStorageBasePath = "/storage/v1/object/public";

        private static HttpClient CreateHttpClient() => new();

        private static async Task<string> GetCdragonVersionAsync()
        {
            if (_cdragonVersion == null)
            {
                try
                {
                    var metaUrl = $"{CdragonBaseUrl}/content-metadata.json";
                    using var request = new HttpRequestMessage(HttpMethod.Get, metaUrl);
                    using var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    using var json = await response.Content.ReadAsStreamAsync();
                    var metadata = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(json, _jsonOptions);
                    if (metadata != null && metadata.TryGetValue("version", out var versionElement))
                    {
                        _cdragonVersion = versionElement.GetString() ?? "latest";
                    }
                    else { _cdragonVersion = "latest"; }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CdragonDataService] Error fetching CDRAGON version: {ex.Message}");
                    _cdragonVersion = "latest";
                }
            }
            return _cdragonVersion;
        }

        private static async Task<T?> FetchDataAsync<T>(string fullUrl, bool isSupabase = false) where T : class
        {
            var httpClientToUse = _httpClient;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                if (isSupabase)
                {
                    request.Headers.TryAddWithoutValidation("apikey", SupabaseAnonKey);
                }

                using var response = await httpClientToUse.SendAsync(request);
                response.EnsureSuccessStatusCode();
                byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();
                if (contentBytes == null || contentBytes.Length == 0) return null;
                using var memoryStream = new MemoryStream(contentBytes);
                return await JsonSerializer.DeserializeAsync<T>(memoryStream, _jsonOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CdragonDataService] Error fetching/parsing {Path.GetFileNameWithoutExtension(fullUrl)}: {ex.Message}");
            }
            return null;
        }

        private static async Task<SupabaseChampionData?> FetchChampionDataFromSupabaseAsync(int championId)
        {
            string supabaseFileUrl = $"{SupabaseUrl}{SupabaseStorageBasePath}/api_json/{championId}.json";
            return await FetchDataAsync<SupabaseChampionData>(supabaseFileUrl, isSupabase: true);
        }

        public static async Task<List<ChampionSummary>?> GetChampionSummariesAsync()
        {
            _ = await GetCdragonVersionAsync();
            var url = $"{DataRoot}/v1/champion-summary.json";
            var summaries = await FetchDataAsync<List<ChampionSummary>>(url);
            return summaries?.Where(c => c.Id != -1).OrderBy(c => c.Name).ToList();
        }

        public static async Task<Dictionary<string, Skin>?> GetAllSkinsAsync()
        {
            _ = await GetCdragonVersionAsync();
            var url = $"{DataRoot}/v1/skins.json";
            return await FetchDataAsync<Dictionary<string, Skin>>(url);
        }

        public static async Task EnrichSkinWithSupabaseChromaDataAsync(Skin wpfSkinToEnrich)
        {
            if (wpfSkinToEnrich == null) return;

            var championDataFromSupabase = await FetchChampionDataFromSupabaseAsync(wpfSkinToEnrich.ChampionId);

            if (championDataFromSupabase?.Skins == null)
            {
                return;
            }

            SupabaseSkinData? supabaseSkinData = championDataFromSupabase.Skins.FirstOrDefault(s => s.Id == wpfSkinToEnrich.Id);

            if (supabaseSkinData?.Chromas != null && supabaseSkinData.Chromas.Any())
            {
                var newWpfChromas = new List<Chroma>();
                foreach (var supabaseChromaSource in supabaseSkinData.Chromas)
                {
                    if (supabaseChromaSource != null)
                    {
                        var newWpfChroma = new Models.Chroma
                        {
                            Id = supabaseChromaSource.Id,
                            Name = supabaseChromaSource.Name,
                            ChromaPath = supabaseChromaSource.ChromaPath,
                            Colors = supabaseChromaSource.Colors != null ? new List<string>(supabaseChromaSource.Colors) : null
                        };
                        newWpfChromas.Add(newWpfChroma);
                    }
                }
                wpfSkinToEnrich.Chromas = newWpfChromas.OrderBy(c => c.Id).ToList();
            }
            else
            {
            }
        }

        public static async Task<ChampionDetail?> GetChampionDetailsAsync(int championId)
        {
            _ = await GetCdragonVersionAsync();
            var detailsUrl = $"{DataRoot}/v1/champions/{championId}.json";
            var championDetailWpf = await FetchDataAsync<ChampionDetail>(detailsUrl);
            if (championDetailWpf == null) return null;

            var allSkinsFromCdragon = await GetAllSkinsAsync();
            var skinsForThisChampionWpf = new List<Skin>();

            if (allSkinsFromCdragon != null)
            {
                foreach (var cdragonSkinEntry in allSkinsFromCdragon.Where(kvp => kvp.Value.ChampionId == championId))
                {
                    Skin cdragonSkinObject = cdragonSkinEntry.Value;

                    var currentWpfSkin = new Skin
                    {
                        Id = cdragonSkinObject.Id,
                        Name = cdragonSkinObject.Name,
                        TilePath = cdragonSkinObject.TilePath,
                        SplashPath = cdragonSkinObject.SplashPath,
                        RarityGemPath = cdragonSkinObject.RarityGemPath,
                        IsLegacy = cdragonSkinObject.IsLegacy,
                        Description = cdragonSkinObject.Description,
                        Chromas = []
                    };

                    if (cdragonSkinObject.Chromas != null && cdragonSkinObject.Chromas.Any())
                    {
                        foreach (var cdragonChromaSource in cdragonSkinObject.Chromas)
                        {
                            if (cdragonChromaSource != null)
                            {
                                currentWpfSkin.Chromas.Add(new Models.Chroma
                                {
                                    Id = cdragonChromaSource.Id,
                                    Name = cdragonChromaSource.Name,
                                    ChromaPath = cdragonChromaSource.ChromaPath,
                                    Colors = cdragonChromaSource.Colors != null ? new List<string>(cdragonChromaSource.Colors) : null
                                });
                            }
                        }
                    }

                    await EnrichSkinWithSupabaseChromaDataAsync(currentWpfSkin);

                    currentWpfSkin.Chromas = currentWpfSkin.Chromas.OrderBy(c => c.Id).ToList();
                    skinsForThisChampionWpf.Add(currentWpfSkin);
                }
            }
            championDetailWpf.Skins = skinsForThisChampionWpf.OrderBy(s => s.Name).ToList();
            return championDetailWpf;
        }

        public static string GetAssetUrl(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "pack://application:,,,/Assets/placeholder.png";
            if (Uri.TryCreate(relativePath, UriKind.Absolute, out Uri? uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                return relativePath;
            const string apiAssetPrefix = "/lol-game-data/assets";
            if (relativePath.StartsWith(apiAssetPrefix, StringComparison.OrdinalIgnoreCase))
                return $"{DataRoot}/{relativePath[apiAssetPrefix.Length..].TrimStart('/')}".ToLowerInvariant();
            return $"{DataRoot}/{relativePath.TrimStart('/')}".ToLowerInvariant();
        }
    }
}