using skinhunter.Models;
using skinhunter.Services;
using skinhunter.ViewModels.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace skinhunter.ViewModels.Pages
{
    public partial class InstalledSkinsViewModel : ViewModelBase, INavigationAware
    {
        private readonly UserPreferencesService _userPreferencesService;
        private readonly ModToolsService _modToolsService;
        private readonly MainWindowViewModel _mainVM;

        [ObservableProperty]
        private ObservableCollection<InstalledSkinInfoDisplay> _installedSkins = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasInstalledSkins))]
        private bool _isLoadingSkinsList = true;

        [ObservableProperty]
        private bool _canUninstallSelected;

        public bool HasInstalledSkins => InstalledSkins.Any();

        public InstalledSkinsViewModel(UserPreferencesService userPreferencesService, ModToolsService modToolsService, MainWindowViewModel mainWindowViewModel)
        {
            _userPreferencesService = userPreferencesService;
            _modToolsService = modToolsService;
            _mainVM = mainWindowViewModel;
        }

        private void UpdateCanUninstall()
        {
            CanUninstallSelected = InstalledSkins.Any(s => s.IsSelected);
            Application.Current.Dispatcher.Invoke(() => {
                UninstallSelectedCommand.NotifyCanExecuteChanged();
                UninstallAllCommand.NotifyCanExecuteChanged();
            });
        }

        public async Task OnNavigatedToAsync()
        {
            IsLoadingSkinsList = true;
            await Task.Delay(200);
            await LoadInstalledSkinsAsync();
        }

        public Task OnNavigatedFromAsync()
        {
            foreach (var skinDisplay in InstalledSkins)
            {
                skinDisplay.SelectionChanged -= OnSkinSelectionChanged;
            }
            InstalledSkins.Clear();
            IsLoadingSkinsList = true;
            return Task.CompletedTask;
        }

        public void OnNavigatedTo(object? _) { }

        private void OnSkinSelectionChanged(object? sender, System.EventArgs e)
        {
            UpdateCanUninstall();
        }

        [RelayCommand]
        private async Task RefreshCommand()
        {
            IsLoadingSkinsList = true;
            await Task.Delay(200);
            await LoadInstalledSkinsAsync();
        }

        private async Task LoadInstalledSkinsAsync()
        {
            try
            {
                InstalledSkins.Clear();
                await _userPreferencesService.LoadPreferencesAsync();
                var skinsFromPrefs = _userPreferencesService.GetInstalledSkins();
                var champSummaries = await CdragonDataService.GetChampionSummariesAsync();
                var championNameMap = champSummaries?.ToDictionary(c => c.Id, c => c.Name) ?? [];

                var displaySkins = skinsFromPrefs
                    .OrderBy(s => s.SkinName)
                    .ThenBy(s => s.ChromaName)
                    .Select(skinInfo =>
                    {
                        var displaySkin = new InstalledSkinInfoDisplay(skinInfo)
                        {
                            ChampionName = championNameMap.GetValueOrDefault(skinInfo.ChampionId, $"Champ ID: {skinInfo.ChampionId}")
                        };
                        displaySkin.SelectionChanged += OnSkinSelectionChanged;
                        return displaySkin;
                    });

                foreach (var skin in displaySkins)
                {
                    InstalledSkins.Add(skin);
                }
            }
            finally
            {
                IsLoadingSkinsList = false;
                UpdateCanUninstall();
            }
        }

        [RelayCommand(CanExecute = nameof(CanUninstallSelected))]
        private async Task UninstallSelected()
        {
            var selectedSkinsInfo = InstalledSkins.Where(s => s.IsSelected).Select(s => s.SkinInfo).ToList();
            if (!selectedSkinsInfo.Any()) return;

            var result = await ShowConfirmationDialogAsync($"Are you sure you want to uninstall {selectedSkinsInfo.Count} selected skin(s)?");

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                _mainVM.IsGloballyLoading = true;
                _mainVM.GlobalLoadingMessage = $"Uninstalling {selectedSkinsInfo.Count} skin(s)...";
                await _modToolsService.QueueUninstallSkins(selectedSkinsInfo);
                await _mainVM.ShowGlobalSuccess("Selected skins uninstalled.");
                await RefreshCommand();
            }
        }

        [RelayCommand(CanExecute = nameof(HasInstalledSkins))]
        private async Task UninstallAll()
        {
            var allSkinsInfo = InstalledSkins.Select(s => s.SkinInfo).ToList();
            if (!allSkinsInfo.Any()) return;

            var result = await ShowConfirmationDialogAsync("Are you sure you want to uninstall ALL installed skins?");

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                _mainVM.IsGloballyLoading = true;
                _mainVM.GlobalLoadingMessage = "Uninstalling all skins...";
                await _modToolsService.QueueUninstallSkins(allSkinsInfo);
                await _mainVM.ShowGlobalSuccess("All skins uninstalled.");
                await RefreshCommand();
            }
        }

        private static async Task<Wpf.Ui.Controls.MessageBoxResult> ShowConfirmationDialogAsync(string content)
        {
            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Confirm Uninstall",
                Content = content + "\nThis operation will stop the mod overlay and rebuild the game configuration.",
                PrimaryButtonText = "Uninstall",
                CloseButtonText = "Cancel",
                PrimaryButtonAppearance = ControlAppearance.Danger
            };
            return await messageBox.ShowDialogAsync();
        }
    }
}