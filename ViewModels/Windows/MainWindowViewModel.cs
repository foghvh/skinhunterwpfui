using System.Collections.ObjectModel;
using Wpf.Ui.Controls;
using skinhunter.Services;
using Microsoft.Extensions.DependencyInjection;
using skinhunter.ViewModels.Dialogs;

namespace skinhunter.ViewModels.Windows
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private ICustomNavigationService? _customNavigationService;

        [ObservableProperty]
        private string _applicationTitle = "Skin Hunter";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems;

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems;

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems;

        [ObservableProperty]
        private ViewModelBase? _dialogViewModel;

        [ObservableProperty]
        private OmnisearchViewModel? _omnisearchDialogViewModel;

        public MainWindowViewModel()
        {
            _menuItems = new ObservableCollection<object>
            {
                new NavigationViewItem() { Content = "Champions (Design)", Icon = new SymbolIcon { Symbol = SymbolRegular.AppsListDetail24 } },
                new NavigationViewItem() { Content = "Search (Design)", Icon = new SymbolIcon { Symbol = SymbolRegular.Search24 } },
                new NavigationViewItem() { Content = "Installed (Design)", Icon = new SymbolIcon { Symbol = SymbolRegular.Save24 } },
                new NavigationViewItem() { Content = "Profile (Design)", Icon = new SymbolIcon { Symbol = SymbolRegular.Person24 } }
            };
            _footerMenuItems = new ObservableCollection<object>
            {
                new NavigationViewItem() { Content = "Settings (Design)", Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 } }
            };
            _trayMenuItems = new ObservableCollection<MenuItem>
            {
                new MenuItem { Header = "Champions (Design)" }
            };
        }

        public MainWindowViewModel(ICustomNavigationService customNavigationService) : this()
        {
            _customNavigationService = customNavigationService;

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
                    Command = new RelayCommand(() => _customNavigationService.ShowOmnisearchDialog())
                },
                new NavigationViewItem()
                {
                    Content = "Installed",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Save24 },
                    TargetPageType = typeof(Views.Pages.DashboardPage)
                },
                 new NavigationViewItem()
                {
                    Content = "Profile",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Person24 },
                    TargetPageType = typeof(Views.Pages.DataPage)
                }
            };

            _footerMenuItems = new ObservableCollection<object>()
            {
                new NavigationViewItem()
                {
                    Content = "Settings",
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                    TargetPageType = typeof(Views.Pages.SettingsPage)
                }
            };
        }
    }
}