﻿using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using skinhunter.Services;
using skinhunter.ViewModels;
using skinhunter.ViewModels.Dialogs;
using skinhunter.ViewModels.Pages;
using skinhunter.Views.Pages;
using Wpf.Ui.DependencyInjection;
using Wpf.Ui;
using Supabase;
using skinhunter.ViewModels.Windows;
using skinhunter.Views.Windows;
using System.Windows;
using System;
using Wpf.Ui.Abstractions;

namespace skinhunter
{
    public partial class App
    {
        public static string? InitialPipeName { get; private set; }

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
                // Register the Page Provider
                services.AddNavigationViewPageProvider();

                // Register Core Services
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<ISnackbarService, SnackbarService>();
                services.AddSingleton<IContentDialogService, ContentDialogService>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Register Application Host Service
                services.AddHostedService<ApplicationHostService>();

                // Register Custom Application Services
                services.AddSingleton<ICustomNavigationService, CustomNavigationService>();
                services.AddSingleton<AuthTokenManager>();
                services.AddTransient<PipeClientService>();
                services.AddSingleton<ModToolsService>();
                services.AddSingleton<UserPreferencesService>();

                // Register Supabase Client
                services.AddSingleton(sp => {
                    var options = new Supabase.SupabaseOptions
                    {
                        AutoRefreshToken = false,
                    };
                    return new Supabase.Client(
                        "https://odlqwkgewzxxmbsqutja.supabase.co",
                        "eyJhbGciOiJzdXBhYmFzZSIsInJlZiI6Im9kbHF3a2dld3p4eG1ic3F1dGphIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyMTM2NzcsImV4cCI6MjA0OTc4OTY3N30.qka6a71bavDeUQgy_BKoVavaClRQa_gT36Au7oO9AF0",
                        options);
                });

                // Register Main Window and its ViewModel
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<INavigationWindow>(sp => sp.GetRequiredService<MainWindow>());

                // Register Page ViewModels
                services.AddSingleton<ChampionGridPageViewModel>();
                services.AddSingleton<ChampionDetailPageViewModel>();
                services.AddSingleton<InstalledSkinsViewModel>();
                services.AddSingleton<ProfileViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddTransient<AuthenticationRequiredPageViewModel>();

                // Register Pages
                services.AddTransient<ChampionGridPage>();
                services.AddTransient<ChampionDetailPage>();
                services.AddTransient<InstalledSkinsPage>();
                services.AddTransient<ProfilePage>();
                services.AddTransient<SettingsPage>();
                services.AddTransient<AuthenticationRequiredPage>();

                // Register Dialog/Component ViewModels
                services.AddTransient<OmnisearchViewModel>();
                services.AddTransient<SkinDetailViewModel>();
                services.AddSingleton<OverlayToggleButtonViewModel>();

            }).Build();

        public static IServiceProvider Services => _host.Services;

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            FileLoggerService.Log($"[App.OnStartup] skinhunter starting. Args: {string.Join(" ", e.Args)}");
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (e.Args[i].Equals("--pipe-name", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    InitialPipeName = e.Args[i + 1];
                    FileLoggerService.Log($"[App.OnStartup] Pipe name received: {InitialPipeName}");
                    break;
                }
            }
            await _host.StartAsync();
        }

        private async void OnExit(object sender, ExitEventArgs e)
        {
            FileLoggerService.Log($"[App.OnExit] skinhunter exiting.");
            var modToolsService = Services.GetService<ModToolsService>();
            if (modToolsService != null)
            {
                await modToolsService.StopRunOverlayAsync();
            }

            await _host.StopAsync();
            _host.Dispose();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            FileLoggerService.Log($"[App.OnDispatcherUnhandledException] Unhandled exception: {e.Exception}");
            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Unhandled Application Error",
                Content = $"An unexpected error occurred: {e.Exception.Message}\n\nThe application might be unstable. Please check logs for details.",
                CloseButtonText = "OK"
            };
            _ = messageBox.ShowDialogAsync();
            e.Handled = true;
        }
    }
}