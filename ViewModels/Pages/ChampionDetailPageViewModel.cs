using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Wpf.Ui.Abstractions.Controls; // Para INavigationAware
using Wpf.Ui.Controls; // Para INavigationViewItem si fuera necesario, aunque no directamente aquí
using Microsoft.Extensions.DependencyInjection; // Para IServiceProvider en el constructor si se necesita


namespace skinhunter.ViewModels.Pages
{
    public partial class ChampionDetailPageViewModel : ViewModelBase, INavigationAware
    {
        private readonly ICustomNavigationService _customNavigationService; // Para otras acciones de navegación personalizadas
        // No se necesita INavigationService aquí si los parámetros se reciben a través de OnNavigatedToAsync

        [ObservableProperty]
        private ChampionDetail? _champion;

        [ObservableProperty]
        private ObservableCollection<Skin> _skins = [];

        private int _currentChampionId = -1;
        private object? _navigationParameter; // Para almacenar el parámetro recibido

        public ChampionDetailPageViewModel(ICustomNavigationService customNavigationService)
        {
            _customNavigationService = customNavigationService;
        }

        // OnNavigatedToAsync es llamado por el sistema de navegación de WPF UI
        // El INavigationService (o el control de navegación) pasa el parámetro aquí.
        public async Task OnNavigatedToAsync()
        {
            if (_navigationParameter is int champId)
            {
                _currentChampionId = champId;
                await LoadChampionAsync(_currentChampionId);
                _navigationParameter = null; // Consumir el parámetro
            }
            else if (_currentChampionId != -1 && (Champion == null || Champion.Id != _currentChampionId || !Skins.Any()))
            {
                // Esto podría ser una recarga si la página ya estaba en el stack y se navega de nuevo a ella sin nuevo parámetro
                await LoadChampionAsync(_currentChampionId);
            }
            // Si no hay parámetro y no hay ID actual, podría ser un error o una navegación no deseada
            // Podrías decidir ir atrás o mostrar un mensaje. Por ahora, se quedará en blanco si es el caso.
        }

        public Task OnNavigatedFromAsync()
        {
            // Aquí podrías limpiar recursos si la página se va del stack
            return Task.CompletedTask;
        }

        // Este método es crucial y es llamado por el INavigationView
        public void OnNavigatedTo(object? parameter)
        {
            _navigationParameter = parameter; // Almacenar el parámetro para usarlo en OnNavigatedToAsync
        }


        public void ReleaseResourcesForTray()
        {
            IsLoading = true;
            Champion?.ReleaseImage();
            Champion = null;
            Skins.Clear();
            IsLoading = false;
        }

        public void PrepareForReload()
        {
            IsLoading = true;
            Champion?.ReleaseImage();
            Champion = null;
            Skins.Clear();
        }

        [RelayCommand]
        public async Task LoadChampionAsync(int championId)
        {
            _currentChampionId = championId;
            if (Champion?.Id == championId && Skins.Any())
            {
                IsLoading = false;
                return;
            }

            IsLoading = true;

            if (Champion?.Id != championId || !Skins.Any())
            {
                Skins.Clear();
                Champion?.ReleaseImage();
                Champion = null;
            }

            var details = await CdragonDataService.GetChampionDetailsAsync(championId);

            if (details != null)
            {
                Champion = details;

                if (details.Skins != null && details.Skins.Any())
                {
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