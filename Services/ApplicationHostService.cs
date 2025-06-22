using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using skinhunter.Views.Pages;
using System;
using System.Windows;
using skinhunter.ViewModels.Windows;

namespace skinhunter.Services
{
    public class ApplicationHostService(IServiceProvider serviceProvider) : IHostedService
    {
        private INavigationWindow? _navigationWindow;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var mainVM = serviceProvider.GetRequiredService<MainWindowViewModel>();
                mainVM.IsGloballyLoading = true;
                mainVM.GlobalLoadingMessage = "Initializing...";

                _navigationWindow = serviceProvider.GetRequiredService<INavigationWindow>();
                _navigationWindow.ShowWindow();
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    var authTokenManager = serviceProvider.GetRequiredService<AuthTokenManager>();
                    var navigationService = serviceProvider.GetRequiredService<INavigationService>();

                    string? receivedToken = null;
                    if (!string.IsNullOrEmpty(App.InitialPipeName))
                    {
                        await SetGlobalLoadingMessageAsync("Authenticating...");
                        var pipeClient = serviceProvider.GetRequiredService<PipeClientService>();
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                        receivedToken = await pipeClient.RequestTokenFromServerAsync(App.InitialPipeName, cts.Token);
                    }

                    bool isAuthenticated = !string.IsNullOrEmpty(receivedToken) && authTokenManager.SetToken(receivedToken);

                    if (isAuthenticated)
                    {
                        await SetGlobalLoadingMessageAsync("Loading user profile...");
                        var userPrefsService = serviceProvider.GetRequiredService<UserPreferencesService>();
                        await Application.Current.Dispatcher.InvokeAsync(userPrefsService.LoadPreferencesAsync);

                        await SetGlobalLoadingMessageAsync("Preparing mods...");
                        var modToolsService = serviceProvider.GetRequiredService<ModToolsService>();
                        await modToolsService.QueueRebuildWithInstalledSkins();

                        await Application.Current.Dispatcher.InvokeAsync(() => navigationService.Navigate(typeof(ChampionGridPage)));
                    }
                    else
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() => navigationService.Navigate(typeof(AuthenticationRequiredPage)));
                    }
                }
                catch (Exception ex)
                {
                    FileLoggerService.Log($"[AppHostService] Critical startup error: {ex}");
                }
                finally
                {
                    await SetGlobalLoadingMessageAsync("", false);
                }
            }, cancellationToken);
        }

        private async Task SetGlobalLoadingMessageAsync(string message, bool isLoading = true)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var mainVM = serviceProvider.GetRequiredService<MainWindowViewModel>();
                mainVM.GlobalLoadingMessage = message;
                mainVM.IsGloballyLoading = isLoading;
            });
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var modToolsService = serviceProvider.GetRequiredService<ModToolsService>();
            await modToolsService.StopRunOverlayAsync();
            await Application.Current.Dispatcher.InvokeAsync(Application.Current.Shutdown);
        }
    }
}