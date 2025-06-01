using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using skinhunter.Services; // Si tu namespace raíz es 'skinhunter'
// Asegúrate que los siguientes using coincidan con tu estructura y namespace raíz
using skinhunter.ViewModels.Pages;
using skinhunter.ViewModels.Windows;
using skinhunter.ViewModels.Dialogs;
using skinhunter.Views.Pages;
using skinhunter.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

// Si tu namespace raíz fuera, por ejemplo, "MyCoolApp", entonces sería:
// using MyCoolApp.Services;
// using MyCoolApp.ViewModels.Pages;
// etc.

namespace skinhunter // Este debe coincidir con el Espacio de nombres predeterminado de tu proyecto
{
    public partial class App
    {
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => {
                string? basePath = Path.GetDirectoryName(AppContext.BaseDirectory);
                if (basePath != null)
                {
                    c.SetBasePath(basePath);
                }
            })
            .ConfigureServices((context, services) =>
            {
                services.AddNavigationViewPageProvider();
                services.AddSingleton<INavigationService, NavigationService>();

                services.AddHostedService<ApplicationHostService>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<ITaskBarService, TaskBarService>();

                services.AddSingleton<ICustomNavigationService, CustomNavigationService>();

                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                // ViewModels de Páginas
                services.AddSingleton<ChampionGridPageViewModel>();
                services.AddSingleton<ChampionDetailPageViewModel>();
                services.AddSingleton<DashboardViewModel>(); // Aquí es donde falla si el using/namespace es incorrecto
                services.AddSingleton<DataViewModel>();       // Aquí es donde falla
                services.AddSingleton<SettingsViewModel>();   // Aquí es donde falla

                // Vistas de Páginas
                services.AddTransient<ChampionGridPage>();
                services.AddTransient<ChampionDetailPage>();
                services.AddTransient<DashboardPage>();
                services.AddTransient<DataPage>();
                services.AddTransient<SettingsPage>();

                // ViewModels de Diálogos
                services.AddTransient<OmnisearchViewModel>();
                services.AddTransient<SkinDetailViewModel>();

            }).Build();

        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();
        }

        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
        }
    }
}