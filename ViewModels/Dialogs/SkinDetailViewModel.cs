using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Supabase;
using System.IO;
using System;
using System.Windows;

namespace skinhunter.ViewModels.Dialogs
{
    public partial class SkinDetailViewModel : ViewModelBase
    {
        private readonly ICustomNavigationService _customNavigationService;
        private readonly UserPreferencesService _userPreferencesService;
        private readonly Client _supabaseClient;
        private readonly ModToolsService _modToolsService;

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
            UserPreferencesService userPreferencesService,
            Client supabaseClient,
            ModToolsService modToolsService)
        {
            _customNavigationService = customNavigationService;
            _userPreferencesService = userPreferencesService;
            _supabaseClient = supabaseClient;
            _modToolsService = modToolsService;

            _userPreferencesService.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(UserPreferencesService.CurrentProfile))
                {
                    UpdateCanUserDownload();
                }
            };
            UpdateCanUserDownload();
        }

        private void UpdateCanUserDownload()
        {
            CanUserDownload = _userPreferencesService.CurrentProfile?.IsBuyer ?? false;
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
            if (skin.Chromas != null && skin.Chromas.Count > 0)
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

        public bool CanDownloadExecute()
        {
            var mainVM = App.Services.GetRequiredService<MainWindowViewModel>();
            return CanUserDownload && SelectedSkin != null && !IsLoading && !mainVM.IsGloballyLoading;
        }

        [RelayCommand(CanExecute = nameof(CanDownloadExecute))]
        private void DownloadSkin()
        {
            if (SelectedSkin == null) return;

            _ = Task.Run(async () =>
            {
                var installedSkins = _userPreferencesService.GetInstalledSkins();
                var existingSkinForChampion = installedSkins.FirstOrDefault(s => s.ChampionId == SelectedSkin.ChampionId);
                bool proceed = true;

                if (existingSkinForChampion != null)
                {
                    proceed = false;
                    string existingSkinDisplayName = string.IsNullOrEmpty(existingSkinForChampion.ChromaName) ? existingSkinForChampion.SkinName : $"{existingSkinForChampion.SkinName} ({existingSkinForChampion.ChromaName})";
                    var confirmResult = await Application.Current.Dispatcher.Invoke(async () =>
                        await new Wpf.Ui.Controls.MessageBox
                        {
                            Title = "Warning",
                            Content = $"You already have '{existingSkinDisplayName}' installed for this champion.\nInstalling a new skin will replace it. Do you want to continue?",
                            PrimaryButtonText = "Continue",
                            CloseButtonText = "Cancel"
                        }.ShowDialogAsync());
                    if (confirmResult == Wpf.Ui.Controls.MessageBoxResult.Primary)
                    {
                        proceed = true;
                    }
                }

                if (proceed)
                {
                    Application.Current.Dispatcher.Invoke(CloseDialog);
                    var mainVM = App.Services.GetRequiredService<MainWindowViewModel>();
                    mainVM.IsGloballyLoading = true;
                    mainVM.GlobalLoadingMessage = "Downloading skin...";

                    try
                    {
                        await DownloadAndQueueInstall();
                        await mainVM.ShowGlobalSuccess("Skin installation queued! The process will run in the background.");
                    }
                    catch (Exception ex)
                    {
                        FileLoggerService.Log($"[SkinDetailVM] Download failed: {ex.Message}\n{ex.StackTrace}");
                        await Application.Current.Dispatcher.InvokeAsync(async () => {
                            var errorMsgBox = new Wpf.Ui.Controls.MessageBox { Title = "Download Failed", Content = $"Failed to download skin.\nError: {ex.Message}", CloseButtonText = "OK" };
                            await errorMsgBox.ShowDialogAsync();
                        });
                    }
                    finally
                    {
                        mainVM.IsGloballyLoading = false;
                    }
                }
            });
        }

        private async Task DownloadAndQueueInstall()
        {
            if (SelectedSkin == null) return;

            var skinToInstall = SelectedSkin;
            var chromaToInstall = SelectedChroma;

            string skinFolderName = SanitizeFileName(skinToInstall.Name);
            string fantomeFileName = $"{skinFolderName}.fantome";

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

            byte[]? fileBytes = await _supabaseClient.Storage.From("campeones").Download(supabasePath, null);

            if (fileBytes == null || fileBytes.Length == 0)
            {
                throw new Exception($"Failed to download from Supabase Storage or file is empty. Path: {supabasePath}");
            }

            var installedInfo = new InstalledSkinInfo
            {
                ChampionId = skinToInstall.ChampionId,
                SkinOrChromaId = chromaToInstall?.Id ?? skinToInstall.Id,
                FileName = fantomeFileName,
                FolderName = skinFolderName,
                SkinName = skinToInstall.Name,
                ChromaName = chromaToInstall?.Name,
                ImageUrl = chromaToInstall?.ImageUrl ?? skinToInstall.TileImageUrl,
                InstalledAt = DateTime.UtcNow
            };

            await _modToolsService.QueueInstallAndRebuild(installedInfo, fileBytes);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown-skin";
            string sanitized = name.ToLowerInvariant();
            sanitized = Regex.Replace(sanitized, @"[^a-z0-9\s-]", "");
            sanitized = Regex.Replace(sanitized, @"\s+", "-").Trim('-');
            if (sanitized.Length > 80)
            {
                sanitized = sanitized[..80];
            }
            return string.IsNullOrEmpty(sanitized) ? "unknown-skin" : sanitized;
        }

        [RelayCommand]
        private void CloseDialog()
        {
            _customNavigationService.CloseDialog();
        }

        private void SetDefaultSelection()
        {
            SelectedChroma = null;
            RefreshChromaSelections(AvailableChromas, null);
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
                RefreshChromaSelections(AvailableChromas, SelectedChroma);
            }
            DownloadSkinCommand.NotifyCanExecuteChanged();
        }

        private static void RefreshChromaSelections(IEnumerable<Chroma> chromas, Chroma? selected)
        {
            foreach (var ch in chromas)
            {
                ch.IsSelected = ch == selected;
            }
        }
    }
}