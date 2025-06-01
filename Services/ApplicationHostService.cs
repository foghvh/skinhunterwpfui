using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using skinhunter.Views.Pages; // Asegúrate que este using está
using Wpf.Ui;


namespace skinhunter.Services
{
    public class ApplicationHostService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private INavigationWindow? _navigationWindow;

        public ApplicationHostService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await HandleActivationAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        private async Task HandleActivationAsync()
        {
            if (!Application.Current.Windows.OfType<skinhunter.Views.Windows.MainWindow>().Any())
            {
                _navigationWindow = _serviceProvider.GetService<INavigationWindow>();
                _navigationWindow!.ShowWindow();

                // Navegación inicial
                // El INavigationService ya debería estar configurado con el control de navegación
                // en el constructor de MainWindow.
                var navigationService = _serviceProvider.GetRequiredService<INavigationService>();

                // Puedes obtener el primer item del ViewModel de MainWindow si es allí donde los defines
                var mainWindowViewModel = _serviceProvider.GetRequiredService<ViewModels.Windows.MainWindowViewModel>();
                var firstMenuItem = mainWindowViewModel.MenuItems.FirstOrDefault() as Wpf.Ui.Controls.NavigationViewItem;

                if (firstMenuItem?.TargetPageType != null)
                {
                    navigationService.Navigate(firstMenuItem.TargetPageType);
                }
                else
                {
                    // Fallback si no hay items de menú o el primero no es navegable
                    navigationService.Navigate(typeof(Views.Pages.ChampionGridPage));
                }
            }
            await Task.CompletedTask;
        }
    }
}