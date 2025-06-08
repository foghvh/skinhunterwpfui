using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Wpf.Ui.Abstractions.Controls;
using System.IO;
using System.Collections.Generic;

namespace skinhunter.ViewModels.Pages
{
    public partial class InstalledSkinInfoDisplay : ObservableObject
    {
        public InstalledSkinInfo Skin { get; }

        [ObservableProperty]
        private bool _isSelected;

        public string DisplayName => string.IsNullOrEmpty(Skin.ChromaName) || Skin.ChromaName.Equals("Default", StringComparison.OrdinalIgnoreCase)
                                    ? Skin.SkinName
                                    : $"{Skin.SkinName} ({Skin.ChromaName})";
        public string FileName => Skin.FileName;
        public string ImageUrl => Skin.ImageUrl;
        public string ChampionName { get; set; } = "Unknown Champion";

        public InstalledSkinInfoDisplay(InstalledSkinInfo skin)
        {
            Skin = skin;
        }
    }

    public partial class InstalledSkinsViewModel : ViewModelBase, INavigationAware
    {
        private readonly UserPreferencesService _userPreferencesService;
        private readonly ModToolsService _modToolsService;
        private readonly MainWindowViewModel _mainVM;

        [ObservableProperty]
        private ObservableCollection<InstalledSkinInfoDisplay> _installedSkins = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasInstalledSkins))]
        [NotifyPropertyChangedFor(nameof(CanUninstallSelected))]
        private bool _isLoadingSkinsList = true;

        public bool HasInstalledSkins => InstalledSkins.Any();
        public bool CanUninstallSelected => InstalledSkins.Any(s => s.IsSelected);

        public InstalledSkinsViewModel(
            UserPreferencesService userPreferencesService,
            ModToolsService modToolsService,
            MainWindowViewModel mainWindowViewModel)
        {
            _userPreferencesService = userPreferencesService;
            _modToolsService = modToolsService;
            _mainVM = mainWindowViewModel;
        }

        public async Task OnNavigatedToAsync()
        {
            await LoadInstalledSkinsAsync();
        }

        public Task OnNavigatedFromAsync()
        {
            return Task.CompletedTask;
        }

        public void OnNavigatedTo(object? parameter) { }

        [RelayCommand]
        private async Task LoadInstalledSkinsAsync()
        {
            IsLoadingSkinsList = true;
            InstalledSkins.Clear();
            await _userPreferencesService.LoadPreferencesAsync();
            var skinsFromPrefs = _userPreferencesService.GetInstalledSkins();
            List<ChampionSummary>? champSummaries = null;
            try
            {
                champSummaries = await CdragonDataService.GetChampionSummariesAsync();
            }
            catch (Exception ex) { FileLoggerService.Log($"[InstalledSkinsVM] Error loading champ summaries: {ex.Message}"); }

            foreach (var skinInfo in skinsFromPrefs.OrderBy(s => s.SkinName).ThenBy(s => s.ChromaName))
            {
                var displaySkin = new InstalledSkinInfoDisplay(skinInfo);
                if (champSummaries != null)
                {
                    var champ = champSummaries.FirstOrDefault(c => c.Id == skinInfo.ChampionId);
                    displaySkin.ChampionName = champ?.Name ?? $"Champ ID: {skinInfo.ChampionId}";
                }
                InstalledSkins.Add(displaySkin);
            }
            OnPropertyChanged(nameof(HasInstalledSkins));
            UninstallSelectedCommand.NotifyCanExecuteChanged();
            IsLoadingSkinsList = false;
        }

        [RelayCommand(CanExecute = nameof(CanUninstallSelected))]
        private async Task UninstallSelectedAsync()
        {
            var selected = InstalledSkins.Where(s => s.IsSelected).ToList();
            if (!selected.Any()) return;

            var confirmResult = await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Confirm Uninstall",
                Content = $"Are you sure you want to uninstall {selected.Count} selected skin(s)?",
                PrimaryButtonText = "Uninstall",
                CloseButtonText = "Cancel"
            }.ShowDialogAsync();

            if (confirmResult != Wpf.Ui.Controls.MessageBoxResult.Primary) return;

            _mainVM.IsGloballyLoading = true;
            _mainVM.GlobalLoadingMessage = "Uninstalling skins...";
            _modToolsService.StopRunOverlay();

            foreach (var skinDisplay in selected)
            {
                string filePath = Path.Combine(_modToolsService.GetInstalledSkinsDirectory(), skinDisplay.Skin.FileName);
                try
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                    FileLoggerService.Log($"[InstalledSkinsVM] Deleted file: {filePath}");
                }
                catch (Exception ex)
                {
                    FileLoggerService.Log($"[InstalledSkinsVM] Error deleting {filePath}: {ex.Message}");
                }
                await _userPreferencesService.RemoveInstalledSkinAsync(skinDisplay.Skin.ChampionId, skinDisplay.Skin.SkinOrChromaId);
            }

            var remainingSkinsFiles = _userPreferencesService.GetInstalledSkins().Select(s => s.FileName).ToList();
            if (remainingSkinsFiles.Any())
            {
                _mainVM.GlobalLoadingMessage = "Rebuilding overlay...";
                var (overlaySuccess, overlayMsg) = await _modToolsService.MakeOverlayAsync(remainingSkinsFiles);
                FileLoggerService.Log($"[InstalledSkinsVM] MakeOverlay after uninstall: {overlayMsg} (Success: {overlaySuccess})");
                _modToolsService.StartRunOverlay();
            }
            else
            {
                FileLoggerService.Log("[InstalledSkinsVM] No skins left, overlay not rebuilt.");
            }

            await LoadInstalledSkinsAsync();
            _mainVM.IsGloballyLoading = false;
        }

        [RelayCommand]
        private async Task UninstallAllAsync()
        {
            if (!HasInstalledSkins) return;

            var confirmResult = await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Confirm Uninstall All",
                Content = "Are you sure you want to uninstall ALL installed skins?",
                PrimaryButtonText = "Uninstall All",
                CloseButtonText = "Cancel",
                PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
            }.ShowDialogAsync();

            if (confirmResult != Wpf.Ui.Controls.MessageBoxResult.Primary) return;

            _mainVM.IsGloballyLoading = true;
            _mainVM.GlobalLoadingMessage = "Uninstalling all skins...";

            _modToolsService.StopRunOverlay();
            _modToolsService.ClearInstalledSkinsDirectory();
            await _userPreferencesService.ClearAllInstalledSkinsAsync();

            FileLoggerService.Log("[InstalledSkinsVM] All skins uninstalled. Overlay effectively cleared.");

            await LoadInstalledSkinsAsync();
            _mainVM.IsGloballyLoading = false;
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadInstalledSkinsAsync();
        }

        partial void OnIsLoadingSkinsListChanged(bool value)
        {
            OnPropertyChanged(nameof(HasInstalledSkins));
        }
    }
}
