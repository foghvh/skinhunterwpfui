using System.Collections.ObjectModel;
using Wpf.Ui.Controls;
using skinhunter.ViewModels.Pages;
using skinhunter.ViewModels.Dialogs;
using skinhunter.Services;
using skinhunter.Models;
using Microsoft.Extensions.DependencyInjection;

namespace skinhunter.ViewModels.Windows
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ICustomNavigationService _customNavigationService;

        [ObservableProperty]
        private string _applicationTitle = "Skin Hunter";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
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
                Command = new RelayCommand(() => App.Services.GetRequiredService<ICustomNavigationService>().ShowOmnisearchDialog())
            }
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Champions", Tag = "tray_champions" }
        };

        [ObservableProperty]
        private ViewModelBase? _dialogViewModel;

        [ObservableProperty]
        private OmnisearchViewModel? _omnisearchDialogViewModel;


        public MainWindowViewModel(ICustomNavigationService customNavigationService)
        {
            _customNavigationService = customNavigationService;
        }
    }
}