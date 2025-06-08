
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using skinhunter.ViewModels.Windows;
using skinhunter.Views.Pages;
using Supabase;
using System.IO;
using skinhunter.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using Supabase.Gotrue.Exceptions;

namespace skinhunter.Services
{
    public class ApplicationHostService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private INavigationWindow? _navigationWindow;
        private readonly AuthTokenManager _authTokenManager;
        private readonly INavigationService _navigationService;
        private MainWindowViewModel? _mainWindowViewModel;
        private readonly Client _supabaseClient;
        private readonly UserPreferencesService _userPreferencesService;
        private readonly ModToolsService _modToolsService;
        private readonly string _supabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im9kbHF3a2dld3p4eG1ic3F1dGphIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyMTM2NzcsImV4cCI6MjA0OTc4OTY3N30.qka6a71bavDeUQgy_BKoVavaClRQa_gT36Au7oO9AF0";
        private readonly string _supabaseUrl = "https://odlqwkgewzxxmbsqutja.supabase.co";


        public ApplicationHostService(IServiceProvider serviceProvider,
                                      AuthTokenManager authTokenManager,
                                      INavigationService navigationService,
                                      Client supabaseClient,
                                      UserPreferencesService userPreferencesService,
                                      ModToolsService modToolsService)
        {
            _serviceProvider = serviceProvider;
            _authTokenManager = authTokenManager;
            _navigationService = navigationService;
            _supabaseClient = supabaseClient;
            _userPreferencesService = userPreferencesService;
            _modToolsService = modToolsService;
        }

        private MainWindowViewModel MainVM => _mainWindowViewModel ??= _serviceProvider.GetRequiredService<MainWindowViewModel>();

        private void SetGlobalLoadingState(bool isLoading, string message = "Loading...")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainVM.IsGloballyLoading = isLoading;
                MainVM.GlobalLoadingMessage = message;
                FileLoggerService.Log($"[AppHostService] GlobalLoading: {isLoading}, Message: {message}");
            });
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            FileLoggerService.Log($"[AppHostService] StartAsync initiated. InitialPipeName: '{App.InitialPipeName}'");

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _navigationWindow = _serviceProvider.GetRequiredService<INavigationWindow>();
                _navigationWindow!.ShowWindow();
                FileLoggerService.Log($"[AppHostService] MainWindow shown.");
            });

            SetGlobalLoadingState(true, "Initializing...");

            _ = Task.Run(async () =>
            {
                try
                {
                    string? receivedToken = null;

                    if (!string.IsNullOrEmpty(App.InitialPipeName))
                    {
                        SetGlobalLoadingState(true, "Authenticating...");
                        FileLoggerService.Log($"[AppHostService][BG_TASK] Attempting to get token from pipe: {App.InitialPipeName}");
                        var pipeClient = _serviceProvider.GetRequiredService<PipeClientService>();
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                        receivedToken = await pipeClient.RequestTokenFromServerAsync(App.InitialPipeName, cts.Token);
                        FileLoggerService.Log(string.IsNullOrEmpty(receivedToken)
                            ? "[AppHostService][BG_TASK] Failed to receive token from pipe OR token was empty."
                            : "[AppHostService][BG_TASK] Token received from pipe.");
                    }
                    else
                    {
                        FileLoggerService.Log("[AppHostService][BG_TASK] No pipe name provided (direct launch).");
                    }

                    bool authenticationSuccessful = false;
                    if (!string.IsNullOrEmpty(receivedToken))
                    {
                        authenticationSuccessful = _authTokenManager.SetToken(receivedToken);
                        FileLoggerService.Log(authenticationSuccessful
                            ? $"[AppHostService][BG_TASK] Token processed by AuthTokenManager. User claim (sub): {_authTokenManager.GetClaim(ClaimTypes.NameIdentifier)}"
                            : "[AppHostService][BG_TASK] AuthTokenManager failed to process received token.");
                    }

                    if (_authTokenManager.IsAuthenticated)
                    {
                        SetGlobalLoadingState(true, "Loading user profile...");
                        await _userPreferencesService.LoadPreferencesAsync();

                        SetGlobalLoadingState(true, "Fetching champion data...");
                        await Task.Delay(500, cancellationToken);

                        SetGlobalLoadingState(true, "Syncing installed skins & overlay...");
                        await SyncAndPrepareOverlay(cancellationToken);

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _navigationService.Navigate(typeof(ChampionGridPage));
                            FileLoggerService.Log($"[AppHostService][BG_TASK] Authenticated. Navigated to ChampionGridPage.");
                        });
                    }
                    else
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _navigationService.Navigate(typeof(AuthenticationRequiredPage));
                            FileLoggerService.Log($"[AppHostService][BG_TASK] Not authenticated or token processing failed. Navigated to AuthenticationRequiredPage.");
                        });
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    FileLoggerService.Log($"[AppHostService][BG_TASK] StartAsync background task cancelled.");
                }
                catch (Exception ex)
                {
                    FileLoggerService.Log($"[AppHostService][BG_TASK] Exception: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _navigationService.Navigate(typeof(AuthenticationRequiredPage));
                    });
                }
                finally
                {
                    SetGlobalLoadingState(false);
                    FileLoggerService.Log($"[AppHostService][BG_TASK] Background task finished. IsAuthenticated: {_authTokenManager.IsAuthenticated}");
                }
            }, cancellationToken);

            await Task.CompletedTask;
        }

        private async Task SyncAndPrepareOverlay(CancellationToken cancellationToken)
        {
            var skinsFromPrefs = _userPreferencesService.GetInstalledSkins();
            FileLoggerService.Log($"[AppHostService.Sync] Found {skinsFromPrefs.Count} skins in user preferences.");
            _modToolsService.ClearInstalledSkinsDirectory();

            var validFantomeFilesForOverlay = new List<string>();
            if (skinsFromPrefs.Any() && _authTokenManager.IsAuthenticated && !string.IsNullOrEmpty(_authTokenManager.CurrentToken))
            {
                MainVM.GlobalLoadingMessage = $"Downloading {skinsFromPrefs.Count} skin files...";

                foreach (var skinInfo in skinsFromPrefs)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        FileLoggerService.Log("[AppHostService.Sync] Download loop cancelled.");
                        return;
                    }
                    string supabasePath;
                    if (skinInfo.ChromaName != null && skinInfo.ChromaName != "Default")
                    {
                        supabasePath = $"campeones/{skinInfo.ChampionId}/{skinInfo.SkinOrChromaId}.fantome";
                    }
                    else
                    {
                        int skinNum = skinInfo.SkinOrChromaId % 1000;
                        supabasePath = $"campeones/{skinInfo.ChampionId}/{skinNum}.fantome";
                    }

                    try
                    {
                        FileLoggerService.Log($"[AppHostService.Sync] Downloading {supabasePath} to {skinInfo.FileName}");
                        byte[] fileBytes = await _supabaseClient.Storage.From("campeones").Download(supabasePath, (EventHandler<float>?)null);

                        if (fileBytes != null && fileBytes.Length > 0)
                        {
                            string localFilePath = Path.Combine(_modToolsService.GetInstalledSkinsDirectory(), skinInfo.FileName);
                            await File.WriteAllBytesAsync(localFilePath, fileBytes, cancellationToken);
                            validFantomeFilesForOverlay.Add(skinInfo.FileName);
                            FileLoggerService.Log($"[AppHostService.Sync] Downloaded {skinInfo.FileName} successfully.");
                        }
                        else
                        {
                            FileLoggerService.Log($"[AppHostService.Sync] Warning: Downloaded file for {skinInfo.FileName} was empty or null from {supabasePath}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        FileLoggerService.Log($"[AppHostService.Sync] Exception downloading {skinInfo.FileName} from {supabasePath}. Type: {ex.GetType().FullName}, Message: {ex.Message}");
                    }
                }
            }
            else if (!skinsFromPrefs.Any())
            {
                FileLoggerService.Log("[AppHostService.Sync] No skins in preferences to download.");
            }
            else
            {
                FileLoggerService.Log("[AppHostService.Sync] Not authenticated or token missing, cannot download skins.");
            }

            if (cancellationToken.IsCancellationRequested) return;
            _modToolsService.StopRunOverlay();

            if (validFantomeFilesForOverlay.Any())
            {
                MainVM.GlobalLoadingMessage = "Building skin overlay...";
                var (overlaySuccess, overlayMsg) = await _modToolsService.MakeOverlayAsync(validFantomeFilesForOverlay);
                FileLoggerService.Log($"[AppHostService.Sync] MakeOverlay result: {overlayMsg} (Success: {overlaySuccess})");

                if (cancellationToken.IsCancellationRequested) return;
                MainVM.GlobalLoadingMessage = "Starting overlay...";
                var (startSuccess, startMsg) = _modToolsService.StartRunOverlay();
                FileLoggerService.Log($"[AppHostService.Sync] StartRunOverlay result: {startMsg} (Success: {startSuccess})");
            }
            else
            {
                FileLoggerService.Log("[AppHostService.Sync] No valid skin files to build overlay. Overlay not started.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            FileLoggerService.Log($"[AppHostService] StopAsync called.");
            var modToolsService = _serviceProvider.GetService<ModToolsService>();
            modToolsService?.StopRunOverlay();
            return Task.CompletedTask;
        }
    }
}
