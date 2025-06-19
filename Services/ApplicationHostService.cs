using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using skinhunter.Views.Pages;
using System;
using System.Windows;

namespace skinhunter.Services
{
    public class ApplicationHostService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private INavigationWindow? _navigationWindow;
        private readonly AuthTokenManager _authTokenManager;
        private readonly INavigationService _navigationService;

        public ApplicationHostService(IServiceProvider serviceProvider,
                                      AuthTokenManager authTokenManager,
                                      INavigationService navigationService)
        {
            _serviceProvider = serviceProvider;
            _authTokenManager = authTokenManager;
            _navigationService = navigationService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var mainVM = _serviceProvider.GetRequiredService<ViewModels.Windows.MainWindowViewModel>();
                mainVM.IsGloballyLoading = true;
                mainVM.GlobalLoadingMessage = "Initializing...";

                _navigationWindow = _serviceProvider.GetRequiredService<INavigationWindow>();
                _navigationWindow.ShowWindow();

                string? receivedToken = null;
                if (!string.IsNullOrEmpty(App.InitialPipeName))
                {
                    mainVM.GlobalLoadingMessage = "Authenticating...";
                    var pipeClient = _serviceProvider.GetRequiredService<PipeClientService>();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                    receivedToken = await pipeClient.RequestTokenFromServerAsync(App.InitialPipeName, cts.Token);
                }

                if (!string.IsNullOrEmpty(receivedToken) && _authTokenManager.SetToken(receivedToken))
                {
                    mainVM.GlobalLoadingMessage = "Loading user profile...";
                    var userPrefsService = _serviceProvider.GetRequiredService<UserPreferencesService>();
                    await userPrefsService.LoadPreferencesAsync();

                    mainVM.GlobalLoadingMessage = "Preparing mods...";
                    var modToolsService = _serviceProvider.GetRequiredService<ModToolsService>();
                    await modToolsService.QueueRebuildWithInstalledSkins();

                    _navigationService.Navigate(typeof(ChampionGridPage));
                }
                else
                {
                    _navigationService.Navigate(typeof(AuthenticationRequiredPage));
                }

                mainVM.IsGloballyLoading = false;
            });
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var modToolsService = _serviceProvider.GetRequiredService<ModToolsService>();
            await modToolsService.StopRunOverlayAsync();
        }
    }
}