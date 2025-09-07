using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using skinhunter.Views.Pages;
using System;
using System.Windows;
using skinhunter.ViewModels.Windows;
using Supabase;
using System.Threading.Tasks;
using System.Threading;

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
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var mainVM = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                mainVM.IsGloballyLoading = true;
                _navigationWindow = _serviceProvider.GetRequiredService<INavigationWindow>();
                _navigationWindow.ShowWindow();
                await InitializeApplicationAsync(cancellationToken);
            });
        }

        private async Task InitializeApplicationAsync(CancellationToken cancellationToken)
        {
            var supabaseClient = _serviceProvider.GetRequiredService<Client>();
            var authTokenManager = _serviceProvider.GetRequiredService<AuthTokenManager>();
            var navigationService = _serviceProvider.GetRequiredService<INavigationService>();
            var userPrefsService = _serviceProvider.GetRequiredService<UserPreferencesService>();
            var modToolsService = _serviceProvider.GetRequiredService<ModToolsService>();
            var mainVM = _serviceProvider.GetRequiredService<MainWindowViewModel>();

            bool isAuthenticated = false;

            mainVM.GlobalLoadingMessage = "Loading session...";
            await supabaseClient.InitializeAsync();

            if (supabaseClient.Auth.CurrentSession?.AccessToken != null)
            {
                authTokenManager.SetToken(supabaseClient.Auth.CurrentSession.AccessToken);
                isAuthenticated = true;
            }
            else if (!string.IsNullOrEmpty(App.InitialPipeName))
            {
                mainVM.GlobalLoadingMessage = "Authenticating...";
                var pipeClient = _serviceProvider.GetRequiredService<PipeClientService>();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                string? receivedToken = await pipeClient.RequestTokenFromServerAsync(App.InitialPipeName, cts.Token);

                if (!string.IsNullOrEmpty(receivedToken))
                {
                    authTokenManager.SetToken(receivedToken);
                    isAuthenticated = true;
                }
            }

            if (cancellationToken.IsCancellationRequested) return;

            if (isAuthenticated)
            {
                mainVM.GlobalLoadingMessage = "Loading user profile...";
                await userPrefsService.LoadPreferencesAsync();

                navigationService.Navigate(typeof(ChampionGridPage));

                if (userPrefsService.GetSyncOnStart())
                {
                    mainVM.GlobalLoadingMessage = "Synchronizing skins...";
                    await modToolsService.QueueSyncAndRebuild();
                }
            }
            else
            {
                navigationService.Navigate(typeof(AuthenticationRequiredPage));
            }
            mainVM.IsGloballyLoading = false;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var modToolsService = _serviceProvider.GetRequiredService<ModToolsService>();
            await modToolsService.StopRunOverlayAsync();
            await Application.Current.Dispatcher.InvokeAsync(Application.Current.Shutdown);
        }
    }
}