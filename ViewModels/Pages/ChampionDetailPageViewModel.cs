using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Wpf.Ui.Abstractions.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.Generic;
using skinhunter.Views.Components;

namespace skinhunter.ViewModels.Pages
{
    public partial class ChampionDetailPageViewModel : ViewModelBase, INavigationAware
    {
        private readonly ICustomNavigationService _customNavigationService;
        private readonly MainWindowViewModel _mainWindowViewModel;
        private readonly IServiceProvider _serviceProvider;

        private readonly List<Skin> _allSkinsForChampion = [];

        [ObservableProperty]
        private ChampionDetail? _champion;

        public ICollectionView? SkinsView { get; private set; }

        [ObservableProperty]
        private string? _skinSearchText;

        partial void OnSkinSearchTextChanged(string? value)
        {
            SkinsView?.Refresh();
        }

        public ChampionDetailPageViewModel(ICustomNavigationService customNavigationService, IServiceProvider serviceProvider)
        {
            _customNavigationService = customNavigationService;
            _mainWindowViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
            _serviceProvider = serviceProvider;
        }

        public void OnNavigatedTo(object? parameter)
        {
        }

        public async Task OnNavigatedToAsync()
        {
            object? consumedParameter = _customNavigationService.ConsumeNavigationParameter();

            _allSkinsForChampion.Clear();
            Champion = null;
            SkinsView = null;
            OnPropertyChanged(nameof(SkinsView));
            _mainWindowViewModel.CurrentPageTitle = "Loading...";

            var header = _serviceProvider.GetRequiredService<ChampionDetailPageHeader>();
            header.DataContext = this;
            _mainWindowViewModel.CurrentPageHeaderContent = header;

            IsLoading = true;

            if (consumedParameter is int champId && champId != -1)
            {
                await LoadChampionAsync(champId);
                if (Champion != null)
                {
                    _mainWindowViewModel.CurrentPageTitle = Champion.Name;
                }
            }
            else
            {
                _mainWindowViewModel.CurrentPageTitle = "Error";
                IsLoading = false;
            }
        }

        public Task OnNavigatedFromAsync()
        {
            Champion = null;
            _allSkinsForChampion.Clear();
            SkinsView = null;
            SkinSearchText = null;
            OnPropertyChanged(nameof(SkinsView));
            IsLoading = false;
            return Task.CompletedTask;
        }

        private bool FilterSkins(object item)
        {
            if (string.IsNullOrWhiteSpace(SkinSearchText))
            {
                return true;
            }

            return item is Skin skin && skin.Name.Contains(SkinSearchText, StringComparison.OrdinalIgnoreCase);
        }

        private async Task LoadChampionAsync(int championId)
        {
            var details = await CdragonDataService.GetChampionDetailsAsync(championId);

            if (details != null)
            {
                Champion = details;
                if (details.Skins != null)
                {
                    _allSkinsForChampion.Clear();
                    foreach (var skin in details.Skins.Where(s =>
                                !s.Name.Equals(details.Name, StringComparison.OrdinalIgnoreCase) &&
                                !s.Name.Equals($"Base {details.Name}", StringComparison.OrdinalIgnoreCase) &&
                                !s.Name.Contains("Original", StringComparison.OrdinalIgnoreCase)))
                    {
                        _allSkinsForChampion.Add(skin);
                    }
                    SkinsView = CollectionViewSource.GetDefaultView(_allSkinsForChampion);
                    SkinsView.Filter = FilterSkins;
                    OnPropertyChanged(nameof(SkinsView));
                }
            }
            else
            {
                System.Windows.MessageBox.Show($"Failed to load details for Champion ID: {championId}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            IsLoading = false;
        }

        [RelayCommand]
        private void SelectSkin(Skin? skin)
        {
            if (skin != null)
            {
                _customNavigationService.ShowSkinDetailDialog(skin);
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            _customNavigationService.GoBack();
        }
    }
}