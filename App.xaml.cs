using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using skinhunter.Services;
using skinhunter.ViewModels.Pages;
using skinhunter.ViewModels.Windows;
using skinhunter.ViewModels.Dialogs;
using skinhunter.Views.Pages;
using skinhunter.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace skinhunter
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

                services.AddSingleton<ChampionGridPageViewModel>();
                services.AddSingleton<ChampionDetailPageViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<DataViewModel>();
                services.AddSingleton<SettingsViewModel>();

                services.AddTransient<ChampionGridPage>();
                services.AddTransient<ChampionDetailPage>();
                services.AddTransient<DashboardPage>();
                services.AddTransient<DataPage>();
                services.AddTransient<SettingsPage>();

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