using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Wpf.Ui.Abstractions.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace skinhunter.ViewModels.Pages
{
    public partial class ChampionDetailPageViewModel : ViewModelBase, INavigationAware
    {
        private readonly ICustomNavigationService _customNavigationService;
        private readonly MainWindowViewModel _mainWindowViewModel;

        [ObservableProperty]
        private ChampionDetail? _champion;

        [ObservableProperty]
        private ObservableCollection<Skin> _skins = [];

        public ChampionDetailPageViewModel(ICustomNavigationService customNavigationService, IServiceProvider serviceProvider)
        {
            _customNavigationService = customNavigationService;
            _mainWindowViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
        }

        public void OnNavigatedTo(object? parameter)
        {
        }

        public async Task OnNavigatedToAsync()
        {
            object? consumedParameter = _customNavigationService.ConsumeNavigationParameter();

            Skins.Clear();
            Champion = null;
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
                IsLoading = false;
            }
        }

        public Task OnNavigatedFromAsync()
        {
            Champion = null;
            Skins.Clear();
            IsLoading = false;
            return Task.CompletedTask;
        }

        private async Task LoadChampionAsync(int championId)
        {
            var details = await CdragonDataService.GetChampionDetailsAsync(championId);

            if (details != null)
            {
                Champion = details;
                if (details.Skins != null)
                {
                    Skins.Clear();
                    foreach (var skin in details.Skins.Where(s =>
                                !s.Name.Equals(details.Name, StringComparison.OrdinalIgnoreCase) &&
                                !s.Name.Equals($"Base {details.Name}", StringComparison.OrdinalIgnoreCase) &&
                                !s.Name.Contains("Original", StringComparison.OrdinalIgnoreCase)))
                    {
                        Skins.Add(skin);
                    }
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