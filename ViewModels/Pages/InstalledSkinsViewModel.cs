using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Wpf.Ui.Abstractions.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System;
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

        public bool HasInstalledSkins => InstalledSkins.Count > 0;

        private InstalledSkinsViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                _installedSkins.Add(new InstalledSkinInfoDisplay(new InstalledSkinInfo { ChampionId = 1, SkinOrChromaId = 1001, FileName = "test-skin.fantome", FolderName = "test-skin", SkinName = "Test Skin", ChromaName = "Red", ImageUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-tiles/aatrox_0.jpg" }) { ChampionName = "Aatrox" });
                _installedSkins.Add(new InstalledSkinInfoDisplay(new InstalledSkinInfo { ChampionId = 2, SkinOrChromaId = 2001, FileName = "test-skin2.fantome", FolderName = "test-skin2", SkinName = "Second Skin", ChromaName = null, ImageUrl = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-tiles/ahri_0.jpg" }) { ChampionName = "Ahri" });
                _installedSkins[0].IsSelected = true;
                IsLoadingSkinsList = false;
                UpdateCanUninstall();
            }
        }

        public InstalledSkinsViewModel(UserPreferencesService userPreferencesService,
            ModToolsService modToolsService,
            MainWindowViewModel mainWindowViewModel) : this()
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                _userPreferencesService = userPreferencesService;
                _modToolsService = modToolsService;
                _mainVM = mainWindowViewModel;
            }
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
            await LoadInstalledSkinsAsync();
        }

        public Task OnNavigatedFromAsync()
        {
            foreach (var skinDisplay in InstalledSkins)
            {
                skinDisplay.SelectionChanged -= OnSkinSelectionChanged;
            }
            InstalledSkins.Clear();
            return Task.CompletedTask;
        }

        public void OnNavigatedTo(object? _) { }

        private void OnSkinSelectionChanged(object? sender, EventArgs e)
        {
            UpdateCanUninstall();
        }

        [RelayCommand]
        private async Task RefreshCommand()
        {
            await LoadInstalledSkinsAsync();
        }

        private async Task LoadInstalledSkinsAsync()
        {
            IsLoadingSkinsList = true;

            foreach (var skinDisplay in InstalledSkins)
            {
                skinDisplay.SelectionChanged -= OnSkinSelectionChanged;
            }
            InstalledSkins.Clear();

            if (_userPreferencesService == null && !DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                IsLoadingSkinsList = false;
                UpdateCanUninstall();
                OnPropertyChanged(nameof(HasInstalledSkins));
                return;
            }
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                IsLoadingSkinsList = false;
                UpdateCanUninstall();
                OnPropertyChanged(nameof(HasInstalledSkins));
                return;
            }

            try
            {
                await _userPreferencesService.LoadPreferencesAsync();
                var skinsFromPrefs = _userPreferencesService.GetInstalledSkins();

                List<ChampionSummary>? champSummaries = await CdragonDataService.GetChampionSummariesAsync();
                var championNameMap = champSummaries?.ToDictionary<ChampionSummary, int, string>(c => c.Id, c => c.Name) ?? new Dictionary<int, string>();

                var displaySkinsList = new List<InstalledSkinInfoDisplay>();
                foreach (var skinInfo in skinsFromPrefs.OrderBy(s => s.SkinName).ThenBy(s => s.ChromaName))
                {
                    var displaySkin = new InstalledSkinInfoDisplay(skinInfo);
                    if (championNameMap.TryGetValue(skinInfo.ChampionId, out string? champName))
                    {
                        displaySkin.ChampionName = champName;
                    }
                    else
                    {
                        displaySkin.ChampionName = $"Champ ID: {skinInfo.ChampionId}";
                    }

                    displaySkin.SelectionChanged += OnSkinSelectionChanged;
                    displaySkinsList.Add(displaySkin);
                }

                Application.Current.Dispatcher.Invoke(() => {
                    foreach (var skinDisplay in displaySkinsList)
                    {
                        InstalledSkins.Add(skinDisplay);
                    }
                    OnPropertyChanged(nameof(HasInstalledSkins));
                    UpdateCanUninstall();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.InvokeAsync(async () => {
                    var messageBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Error Loading Skins",
                        Content = $"Failed to load installed skins list: {ex.Message}",
                        CloseButtonText = "OK"
                    };
                    await messageBox.ShowDialogAsync();
                });
            }
            finally
            {
                IsLoadingSkinsList = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanUninstallSelected))]
        private void UninstallSelected()
        {
            var selectedSkinsInfo = InstalledSkins.Where(s => s.IsSelected).Select(s => s.SkinInfo).ToList();
            if (selectedSkinsInfo.Count == 0) return;

            _ = Task.Run(async () => {
                var confirmResult = await Application.Current.Dispatcher.Invoke(async () =>
                    await new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Confirm Uninstall",
                        Content = $"Are you sure you want to uninstall {selectedSkinsInfo.Count} selected skin(s)?\nThis operation will stop the mod overlay and rebuild the game configuration.",
                        PrimaryButtonText = "Uninstall",
                        CloseButtonText = "Cancel",
                        PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
                    }.ShowDialogAsync());

                if (confirmResult == Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        _mainVM.IsGloballyLoading = true;
                        _mainVM.GlobalLoadingMessage = $"Uninstalling {selectedSkinsInfo.Count} skin(s)...";
                    });

                    try
                    {
                        await _modToolsService.QueueUninstallSkins(selectedSkinsInfo);

                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await _mainVM.ShowGlobalSuccess("Selected skins uninstalled.");
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.InvokeAsync(async () => {
                            var errorMsgBox = new Wpf.Ui.Controls.MessageBox { Title = "Uninstall Failed", Content = $"Failed to queue skin uninstallation.\nError: {ex.Message}", CloseButtonText = "OK" };
                            await errorMsgBox.ShowDialogAsync();
                        });
                    }
                }
                await Application.Current.Dispatcher.InvokeAsync(LoadInstalledSkinsAsync);
            });
        }

        [RelayCommand(CanExecute = nameof(HasInstalledSkins))]
        private void UninstallAll()
        {
            var allSkinsInfo = InstalledSkins.Select(s => s.SkinInfo).ToList();
            if (allSkinsInfo.Count == 0) return;

            _ = Task.Run(async () => {
                var confirmResult = await Application.Current.Dispatcher.Invoke(async () =>
                    await new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Confirm Uninstall All",
                        Content = $"Are you sure you want to uninstall ALL installed skins ({allSkinsInfo.Count})?\nThis operation will stop the mod overlay and rebuild the game configuration.",
                        PrimaryButtonText = "Uninstall All",
                        CloseButtonText = "Cancel",
                        PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
                    }.ShowDialogAsync());

                if (confirmResult == Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        _mainVM.IsGloballyLoading = true;
                        _mainVM.GlobalLoadingMessage = $"Uninstalling all skins ({allSkinsInfo.Count})...";
                    });

                    try
                    {
                        await _modToolsService.QueueUninstallSkins(allSkinsInfo);

                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await _mainVM.ShowGlobalSuccess("All skins uninstalled.");
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.InvokeAsync(async () => {
                            var errorMsgBox = new Wpf.Ui.Controls.MessageBox { Title = "Uninstall Failed", Content = $"Failed to uninstall all skins.\nError: {ex.Message}", CloseButtonText = "OK" };
                            await errorMsgBox.ShowDialogAsync();
                        });
                    }
                }
                await Application.Current.Dispatcher.InvokeAsync(LoadInstalledSkinsAsync);
            });
        }
    }
}