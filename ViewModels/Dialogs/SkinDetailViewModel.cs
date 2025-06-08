
using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using Supabase;
using System.Net;

namespace skinhunter.ViewModels.Dialogs
{
    public partial class SkinDetailViewModel : ViewModelBase
    {
        private readonly ICustomNavigationService _customNavigationService;
        private readonly AuthTokenManager _authTokenManager;
        private readonly ModToolsService _modToolsService;
        private readonly UserPreferencesService _userPreferencesService;
        private readonly string _supabaseUrl = "https://odlqwkgewzxxmbsqutja.supabase.co";
        private readonly string _supabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im9kbHF3a2dld3p4eG1ic3F1dGphIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyMTM2NzcsImV4cCI6MjA0OTc4OTY3N30.qka6a71bavDeUQgy_BKoVavaClRQa_gT36Au7oO9AF0";


        [ObservableProperty]
        private Skin? _selectedSkin;

        public ObservableCollection<Chroma> AvailableChromas { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDefaultSelected))]
        [NotifyPropertyChangedFor(nameof(KhadaViewerUrl))]
        private Chroma? _selectedChroma;

        [ObservableProperty]
        private bool _canUserDownload;

        public bool IsDefaultSelected => SelectedChroma == null;

        public string? KhadaViewerUrl
        {
            get
            {
                if (SelectedSkin == null) return null;
                int skinId = SelectedSkin.Id;
                int? chromaId = SelectedChroma?.Id;
                string url = $"https://modelviewer.lol/model-viewer?id={skinId}";
                if (chromaId.HasValue && chromaId.Value != 0 && chromaId.Value / 1000 == skinId)
                {
                    url += $"&chroma={chromaId.Value}";
                }
                return url;
            }
        }

        public SkinDetailViewModel(
            ICustomNavigationService customNavigationService,
            AuthTokenManager authTokenManager,
            ModToolsService modToolsService,
            UserPreferencesService userPreferencesService)
        {
            _customNavigationService = customNavigationService;
            _authTokenManager = authTokenManager;
            _modToolsService = modToolsService;
            _userPreferencesService = userPreferencesService;

            _userPreferencesService.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(UserPreferencesService.CurrentProfile))
                {
                    UpdateCanUserDownload();
                }
            };
            UpdateCanUserDownload();
        }

        private void UpdateCanUserDownload()
        {
            if (_userPreferencesService.CurrentProfile != null)
            {
                CanUserDownload = _userPreferencesService.CurrentProfile.IsBuyer;
                FileLoggerService.Log($"[SkinDetailVM] Fetched IsBuyer from UserPreferencesService: {CanUserDownload}");
            }
            else
            {
                CanUserDownload = false;
                FileLoggerService.Log("[SkinDetailVM] UserPreferencesService.CurrentProfile is null. CanUserDownload set to False.");
            }
            DownloadSkinCommand.NotifyCanExecuteChanged();
        }

        public async Task LoadSkinAsync(Skin skin)
        {
            IsLoading = true;
            SelectedSkin = null;
            DownloadSkinCommand.NotifyCanExecuteChanged();

            UpdateCanUserDownload();
            await CdragonDataService.EnrichSkinWithSupabaseChromaDataAsync(skin);

            AvailableChromas.Clear();
            if (skin.Chromas != null && skin.Chromas.Any())
            {
                foreach (var chroma in skin.Chromas)
                {
                    if (chroma != null)
                    {
                        chroma.IsSelected = false;
                        AvailableChromas.Add(chroma);
                    }
                }
            }

            SelectedChroma = null;
            IsLoading = false;
            SelectedSkin = skin;
            DownloadSkinCommand.NotifyCanExecuteChanged();
        }

        public bool CanDownloadExecute() => CanUserDownload && SelectedSkin != null && !IsLoading;

        [RelayCommand(CanExecute = nameof(CanDownloadExecute))]
        private async Task DownloadSkinAsync()
        {
            if (!CanUserDownload || SelectedSkin == null)
            {
                var cantDownloadMsg = new Wpf.Ui.Controls.MessageBox { Title = "Permission Denied", Content = "You may not have permission to download skins, or no skin is selected.", CloseButtonText = "OK" };
                await cantDownloadMsg.ShowDialogAsync();
                return;
            }
            if (string.IsNullOrEmpty(_authTokenManager.CurrentToken))
            {
                var noTokenMsg = new Wpf.Ui.Controls.MessageBox { Title = "Authentication Error", Content = "Authentication token is missing. Please restart.", CloseButtonText = "OK" };
                await noTokenMsg.ShowDialogAsync();
                return;
            }

            IsLoading = true;
            MainVM.IsGloballyLoading = true;
            MainVM.GlobalLoadingMessage = "Downloading skin file...";

            var skinToInstall = SelectedSkin;
            var chromaToInstall = SelectedChroma;

            string skinFantomeNamePart = SanitizeFileName(skinToInstall.Name);
            string chromaFantomeNamePart = chromaToInstall != null ? SanitizeFileName(chromaToInstall.Name) : string.Empty;
            string fantomeFileName = $"{skinFantomeNamePart}{(string.IsNullOrEmpty(chromaFantomeNamePart) ? "" : $"-{chromaFantomeNamePart}")}.fantome";

            string supabasePath;
            if (chromaToInstall != null)
            {
                supabasePath = $"campeones/{skinToInstall.ChampionId}/{chromaToInstall.Id}.fantome";
            }
            else
            {
                int skinNum = skinToInstall.Id % 1000;
                supabasePath = $"campeones/{skinToInstall.ChampionId}/{skinNum}.fantome";
            }

            FileLoggerService.Log($"[SkinDetailVM] Attempting to download from Supabase Storage: bucket 'campeones', path '{supabasePath}' to '{fantomeFileName}'");

            try
            {
                byte[] fileBytes;
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authTokenManager.CurrentToken);
                    httpClient.DefaultRequestHeaders.Add("apikey", _supabaseAnonKey);
                    string downloadUrl = $"{_supabaseUrl}/storage/v1/object/public/campeones/{supabasePath}";
                    FileLoggerService.Log($"[SkinDetailVM] Download URL: {downloadUrl}");

                    HttpResponseMessage response = await httpClient.GetAsync(downloadUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        FileLoggerService.Log($"[SkinDetailVM] HTTP Error downloading: {response.StatusCode} - {errorContent}");
                        throw new HttpRequestException($"Failed to download from Supabase Storage. Status: {response.StatusCode}", null, response.StatusCode);
                    }
                    fileBytes = await response.Content.ReadAsByteArrayAsync();
                }

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    throw new Exception("Downloaded file is empty or null from Supabase Storage.");
                }

                string localFilePath = Path.Combine(_modToolsService.GetInstalledSkinsDirectory(), fantomeFileName);
                await File.WriteAllBytesAsync(localFilePath, fileBytes);
                FileLoggerService.Log($"[SkinDetailVM] Skin downloaded to: {localFilePath}");

                MainVM.GlobalLoadingMessage = "Importing skin...";
                var (importSuccess, importMsg) = await _modToolsService.ImportSkinAsync(fantomeFileName);
                if (!importSuccess) throw new Exception($"Import failed: {importMsg}");
                FileLoggerService.Log($"[SkinDetailVM] Skin import result: {importMsg}");

                var installedInfo = new InstalledSkinInfo
                {
                    ChampionId = skinToInstall.ChampionId,
                    SkinOrChromaId = chromaToInstall?.Id ?? skinToInstall.Id,
                    FileName = fantomeFileName,
                    SkinName = skinToInstall.Name,
                    ChromaName = chromaToInstall?.Name,
                    ImageUrl = chromaToInstall?.ImageUrl ?? skinToInstall.TileImageUrl,
                    InstalledAt = DateTime.UtcNow
                };
                await _userPreferencesService.AddInstalledSkinAsync(installedInfo);

                MainVM.GlobalLoadingMessage = "Updating overlay...";
                var installedSkinsCurrent = _userPreferencesService.GetInstalledSkins().Select(s => s.FileName).ToList();
                var (overlaySuccess, overlayMsg) = await _modToolsService.MakeOverlayAsync(installedSkinsCurrent);
                if (!overlaySuccess) FileLoggerService.Log($"[SkinDetailVM] Warning: MakeOverlay failed: {overlayMsg}");
                else FileLoggerService.Log($"[SkinDetailVM] MakeOverlay result: {overlayMsg}");

                _modToolsService.StopRunOverlay();
                _modToolsService.StartRunOverlay();

                MainVM.IsGloballyLoading = false;
                var successMsg = new Wpf.Ui.Controls.MessageBox { Title = "Success", Content = $"{skinToInstall.Name}{(chromaToInstall != null ? " (" + chromaToInstall.Name + ")" : "")} installed successfully!", CloseButtonText = "OK" };
                await successMsg.ShowDialogAsync();
                CloseDialog();
            }
            catch (Exception ex)
            {
                MainVM.IsGloballyLoading = false;
                string errorType = ex.GetType().FullName ?? "Unknown Exception";
                FileLoggerService.Log($"[SkinDetailVM] Error during skin download/install. Type: {errorType}, Message: {ex.Message}");

                string additionalInfo = "";
                if (ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
                {
                    additionalInfo = $" (HttpStatusCode: {httpEx.StatusCode.Value})";
                }
                var errorMsgBox = new Wpf.Ui.Controls.MessageBox { Title = "Error", Content = $"Failed to install skin: {ex.Message}{additionalInfo}", CloseButtonText = "OK" };
                await errorMsgBox.ShowDialogAsync();
            }
            finally
            {
                IsLoading = false;
                DownloadSkinCommand.NotifyCanExecuteChanged();
                MainVM.IsGloballyLoading = false;
            }
        }

        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown_skin";
            string invalidChars = new string(System.IO.Path.GetInvalidFileNameChars());
            string sanitized = name;
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "_");
            }
            sanitized = sanitized.Replace(" ", "-");
            return Regex.Replace(sanitized, @"[^a-zA-Z0-9\-_.]", "").ToLowerInvariant();
        }

        private MainWindowViewModel MainVM => App.Services.GetRequiredService<MainWindowViewModel>();

        [RelayCommand]
        private void CloseDialog()
        {
            _customNavigationService.CloseDialog();
        }

        private void SetDefaultSelection()
        {
            SelectedChroma = null;
            RefreshChromaSelections(null);
        }

        [RelayCommand]
        private void ToggleChromaSelection(Chroma? clickedChroma)
        {
            if (clickedChroma == null) return;

            if (SelectedChroma == clickedChroma)
            {
                SetDefaultSelection();
            }
            else
            {
                SelectedChroma = clickedChroma;
                RefreshChromaSelections(SelectedChroma);
            }
            DownloadSkinCommand.NotifyCanExecuteChanged();
        }

        private void RefreshChromaSelections(Chroma? selected)
        {
            foreach (var ch in AvailableChromas)
            {
                ch.IsSelected = (ch == selected);
            }
        }
    }
}
