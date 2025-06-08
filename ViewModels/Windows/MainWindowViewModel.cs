using System.Collections.ObjectModel;
using skinhunter.Services;
using Microsoft.Extensions.DependencyInjection;
using skinhunter.ViewModels.Dialogs;
using skinhunter.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Controls;
using System.Windows.Controls;
using System.Windows.Data;
using skinhunter.ViewModels;

namespace skinhunter.ViewModels.Windows
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ICustomNavigationService? _customNavigationService;
        private readonly AuthTokenManager _authTokenManager;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private string _applicationTitle = "Skin Hunter";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems;

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems;

        [ObservableProperty]
        private ObservableCollection<System.Windows.Controls.MenuItem> _trayMenuItems;

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

        public MainWindowViewModel(
            ICustomNavigationService customNavigationService,
            AuthTokenManager authTokenManager,
            INavigationService navigationService,
            OverlayToggleButtonViewModel overlayButtonViewModel)
        {
            _customNavigationService = customNavigationService;
            _authTokenManager = authTokenManager;
            _navigationService = navigationService;
            _authTokenManager.PropertyChanged += AuthTokenManager_PropertyChanged;

            IsAppAuthenticated = _authTokenManager.IsAuthenticated;

            _menuItems = new ObservableCollection<object>()
            {
                new NavigationViewItem()
                {
                    Content = "Champions",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.AppsListDetail24 },
                    TargetPageType = typeof(Views.Pages.ChampionGridPage)
                },
                new NavigationViewItem()
                {
                    Content = "Search",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Search24 },
                    Command = new RelayCommand(() => _customNavigationService?.ShowOmnisearchDialog(), () => IsAppAuthenticated)
                },
                new NavigationViewItem()
                {
                    Content = "Installed",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Apps24 },
                    TargetPageType = typeof(Views.Pages.InstalledSkinsPage)
                },
                 new NavigationViewItem()
                {
                    Content = "Profile",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Person24 },
                    TargetPageType = typeof(Views.Pages.ProfilePage)
                }
            };

            var overlayItem = new NavigationViewItem
            {
                DataContext = overlayButtonViewModel,
                Command = overlayButtonViewModel.ToggleOverlayCommand
            };
            overlayItem.SetBinding(NavigationViewItem.ContentProperty, new Binding("Content"));
            var iconBinding = new Binding("Icon")
            {
                Converter = new Converters.SymbolToIconConverter()
            };
            overlayItem.SetBinding(NavigationViewItem.IconProperty, iconBinding);


            _footerMenuItems = new ObservableCollection<object>()
            {
                overlayItem,
                new NavigationViewItem()
                {
                    Content = "Settings",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                    TargetPageType = typeof(Views.Pages.SettingsPage)
                }
            };

            _trayMenuItems = new ObservableCollection<System.Windows.Controls.MenuItem>
            {
                new System.Windows.Controls.MenuItem { Header = "Open Skin Hunter", Command = new RelayCommand(() => {
                    var navWindow = App.Services.GetRequiredService<INavigationWindow>();
                    navWindow.ShowWindow();
                }) },
                new System.Windows.Controls.MenuItem { Header = "Exit", Command = new RelayCommand(() => Application.Current.Shutdown()) }
            };

            UpdateMenuItemsEnabledState();
        }

        private void AuthTokenManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AuthTokenManager.IsAuthenticated))
            {
                IsAppAuthenticated = _authTokenManager.IsAuthenticated;
                UpdateMenuItemsEnabledState();

                if (IsAppAuthenticated)
                {
                    var navigationControl = _navigationService.GetNavigationControl();
                    var frame = navigationControl?.GetType().GetProperty("Frame")?.GetValue(navigationControl) as Frame;

                    if (frame?.Content is Page pageContent && pageContent.GetType() == typeof(Views.Pages.AuthenticationRequiredPage))
                    {
                        FileLoggerService.Log("[MainWindowViewModel] Authenticated, was on AuthRequired. Navigating to Champions.");
                        _navigationService.Navigate(typeof(Views.Pages.ChampionGridPage));
                    }
                }
            }
        }

        private void UpdateMenuItemsEnabledState()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in MenuItems)
                {
                    if (item is NavigationViewItem navItem)
                    {
                        if (navItem.Command is IRelayCommand relayCommand)
                        {
                            relayCommand.NotifyCanExecuteChanged();
                        }
                        if (navItem.TargetPageType != null)
                        {
                            navItem.IsEnabled = IsAppAuthenticated || navItem.TargetPageType == typeof(Views.Pages.AuthenticationRequiredPage);
                        }
                        else if (navItem.Command != null)
                        {
                            navItem.IsEnabled = IsAppAuthenticated;
                        }
                    }
                }
                foreach (var item in FooterMenuItems)
                {
                    if (item is NavigationViewItem navItem && navItem.TargetPageType != null)
                    {
                        navItem.IsEnabled = IsAppAuthenticated || navItem.TargetPageType == typeof(Views.Pages.AuthenticationRequiredPage);
                    }
                }
            });
        }
    }
}
