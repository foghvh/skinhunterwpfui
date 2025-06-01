using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Wpf.Ui.Abstractions.Controls;

namespace skinhunter.ViewModels.Pages
{
    public partial class ChampionDetailPageViewModel : ViewModelBase, INavigationAware
    {
        private readonly ICustomNavigationService _customNavigationService;

        [ObservableProperty]
        private ChampionDetail? _champion;

        [ObservableProperty]
        private ObservableCollection<Skin> _skins = [];

        // Ya no necesitamos _navigationParameterInternal si lo obtenemos del servicio

        public ChampionDetailPageViewModel(ICustomNavigationService customNavigationService)
        {
            _customNavigationService = customNavigationService;
            Debug.WriteLine($"[ChampionDetailPageViewModel] Constructor. HashCode: {this.GetHashCode()}");
        }

        public void OnNavigatedTo(object? parameter)
        {
            // Aunque no usemos el 'parameter' directamente de aquí si es null,
            // es bueno tener el método por si WPF UI lo necesita para otros propósitos internos.
            // El parámetro real se obtendrá de _customNavigationService.
            Debug.WriteLine($"[ChampionDetailPageViewModel.OnNavigatedTo] (WPF UI Parameter): '{parameter}', Type: {parameter?.GetType().FullName}");
        }

        public async Task OnNavigatedToAsync()
        {
            // Obtener el parámetro del servicio personalizado
            object? consumedParameter = _customNavigationService.ConsumeNavigationParameter();
            Debug.WriteLine($"[ChampionDetailPageViewModel.OnNavigatedToAsync] HashCode: {this.GetHashCode()}. Consumed Parameter from service: '{consumedParameter}', Type: {consumedParameter?.GetType().FullName}");

            Skins.Clear();
            Champion = null;
            IsLoading = true;

            if (consumedParameter is int champId && champId != -1)
            {
                Debug.WriteLine($"[ChampionDetailPageViewModel.OnNavigatedToAsync] Valid championId from service: {champId}. Loading champion...");
                await LoadChampionAsync(champId);
            }
            else
            {
                Debug.WriteLine($"[ChampionDetailPageViewModel.OnNavigatedToAsync] Invalid or missing championId from service. Consumed parameter value: '{consumedParameter}'. Setting IsLoading = false.");
                IsLoading = false;
            }
        }

        public Task OnNavigatedFromAsync()
        {
            Debug.WriteLine($"[ChampionDetailPageViewModel.OnNavigatedFromAsync] HashCode: {this.GetHashCode()}. Navigating away from champion: {Champion?.Name}");
            Champion = null;
            Skins.Clear();
            IsLoading = false;
            // _customNavigationService.ConsumeNavigationParameter(); // Opcional: asegurar que se limpie si no se consumió, aunque ya lo hace al consumir.
            return Task.CompletedTask;
        }

        private async Task LoadChampionAsync(int championId)
        {
            Debug.WriteLine($"[ChampionDetailPageViewModel.LoadChampionAsync] HashCode: {this.GetHashCode()}. Loading champion ID: {championId}");
            var details = await CdragonDataService.GetChampionDetailsAsync(championId);

            if (details != null)
            {
                Champion = details;
                Debug.WriteLine($"[ChampionDetailPageViewModel.LoadChampionAsync] Loaded champion: {Champion?.Name}. Skin count from service: {details.Skins?.Count ?? 0}");
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
                    Debug.WriteLine($"[ChampionDetailPageViewModel.LoadChampionAsync] Filtered skins added. Total skins in collection: {Skins.Count}");
                }
            }
            else
            {
                Debug.WriteLine($"[ChampionDetailPageViewModel.LoadChampionAsync] Failed to load details for Champion ID: {championId}");
                System.Windows.MessageBox.Show($"Failed to load details for Champion ID: {championId}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            IsLoading = false;
            Debug.WriteLine($"[ChampionDetailPageViewModel.LoadChampionAsync] Finished loading. IsLoading: {IsLoading}");
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