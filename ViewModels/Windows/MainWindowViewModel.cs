using System.Collections.ObjectModel;
using skinhunter.Services;
using skinhunter.ViewModels.Dialogs;
using skinhunter.Views.Pages;
using Wpf.Ui.Controls;
using System.Windows.Data;
using System.Linq;
using System;
using System.Windows;
using System.Threading.Tasks;
using System.ComponentModel;
using Wpf.Ui.Abstractions;

namespace skinhunter.ViewModels.Windows
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ICustomNavigationService? _customNavigationService;
        private readonly AuthTokenManager _authTokenManager;
        private readonly INavigationService _navigationService;
        private readonly ModToolsService _modToolsService;

        [ObservableProperty]
        private string _applicationTitle = "Skin Hunter";

        [ObservableProperty]
        private object? _navigationHeader;

        [ObservableProperty]
        private Type? _currentPageType;

        [ObservableProperty]
        private ObservableCollection<object> _menuItems;

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems;

        [ObservableProperty]
        private ViewModelBase? _dialogViewModel;

        [ObservableProperty]
        private OmnisearchViewModel? _omnisearchDialogViewModel;

        [ObservableProperty]
        private bool _isAppAuthenticated;

        [ObservableProperty]
        private bool _isGloballyLoading;

        [ObservableProperty]
        private string _globalLoadingMessage = "Initializing...";

        [ObservableProperty]
        private bool _isGlobalSuccessOverlayVisible;

        [ObservableProperty]
        private string _globalSuccessMessage = "Success";

        [ObservableProperty]
        private ObservableCollection<string> _modToolsOutputLog = new();

        public OverlayToggleButtonViewModel OverlayButtonViewModel { get; }

        public MainWindowViewModel(
            ICustomNavigationService customNavigationService,
            AuthTokenManager authTokenManager,
            INavigationService navigationService,
            OverlayToggleButtonViewModel overlayButtonViewModel,
            ModToolsService modToolsService)
        {
            _customNavigationService = customNavigationService;
            _authTokenManager = authTokenManager;
            _navigationService = navigationService;
            _modToolsService = modToolsService;
            OverlayButtonViewModel = overlayButtonViewModel;

            _authTokenManager.PropertyChanged += AuthTokenManager_PropertyChanged;
            OverlayButtonViewModel.OperationStarted += OnOverlayOperationStarted;
            OverlayButtonViewModel.OperationCompleted += OnOverlayOperationCompleted;
            _modToolsService.CommandOutputReceived += OnModToolsOutputReceived;

            IsAppAuthenticated = _authTokenManager.IsAuthenticated;

            _menuItems =
            [
                new NavigationViewItem { Content = "Home", Icon = new SymbolIcon(SymbolRegular.Home24), TargetPageType = typeof(ChampionGridPage) },
                new NavigationViewItem { Content = "Installed", Icon = new SymbolIcon(SymbolRegular.Apps24), TargetPageType = typeof(InstalledSkinsPage) },
                new NavigationViewItem { Content = "Search", Icon = new SymbolIcon(SymbolRegular.Search24), Command = new RelayCommand(() => _customNavigationService?.ShowOmnisearchDialog()) }
            ];

            _footerMenuItems =
            [
                new NavigationViewItem { Content = "Profile", Icon = new SymbolIcon(SymbolRegular.Person24), TargetPageType = typeof(ProfilePage) },
                new NavigationViewItem { Content = "Settings", Icon = new SymbolIcon(SymbolRegular.Settings24), TargetPageType = typeof(SettingsPage) }
            ];

            UpdateMenuItemsEnabledState();
        }

        private void OnModToolsOutputReceived(string output)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ModToolsOutputLog.Count > 0)
                {
                    ModToolsOutputLog.Clear();
                }
                ModToolsOutputLog.Add(output);
            });
        }

        public void SetCurrentPage(Type pageType)
        {
            CurrentPageType = pageType;
        }

        private void OnOverlayOperationStarted(string message)
        {
            IsGloballyLoading = true;
            GlobalLoadingMessage = message;
            IsGlobalSuccessOverlayVisible = false;
        }

        private void OnOverlayOperationCompleted()
        {
            IsGloballyLoading = false;
        }

        public async Task ShowGlobalSuccess(string message, int displayTimeMs = 2500)
        {
            GlobalSuccessMessage = message;
            IsGlobalSuccessOverlayVisible = true;
            IsGloballyLoading = false;
            await Task.Delay(displayTimeMs);
            IsGlobalSuccessOverlayVisible = false;
        }

        private void AuthTokenManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AuthTokenManager.IsAuthenticated))
            {
                IsAppAuthenticated = _authTokenManager.IsAuthenticated;
                UpdateMenuItemsEnabledState();

                if (IsAppAuthenticated)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (CurrentPageType == typeof(AuthenticationRequiredPage))
                        {
                            _navigationService.Navigate(typeof(ChampionGridPage));
                        }
                    });
                }
            }
        }

        private void UpdateMenuItemsEnabledState()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in _menuItems.Concat(_footerMenuItems))
                {
                    if (item is NavigationViewItem navItem)
                    {
                        bool isAuthDependent = navItem.TargetPageType != typeof(AuthenticationRequiredPage) &&
                                               navItem.TargetPageType != typeof(SettingsPage) &&
                                               navItem.TargetPageType != null;

                        if (navItem.Command != null && navItem.TargetPageType == null)
                        {
                            if (navItem.Content as string == "Search")
                            {
                                isAuthDependent = true;
                            }
                        }

                        navItem.IsEnabled = !isAuthDependent || IsAppAuthenticated;

                        if (navItem.Command is IRelayCommand relayCommand)
                        {
                            relayCommand.NotifyCanExecuteChanged();
                        }
                    }
                }

                OverlayButtonViewModel.ToggleOverlayCommand.NotifyCanExecuteChanged();
            });
        }
    }
}