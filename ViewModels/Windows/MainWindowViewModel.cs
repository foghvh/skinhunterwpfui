using skinhunter.Services;
using skinhunter.ViewModels.Dialogs;
using skinhunter.Views.Pages;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace skinhunter.ViewModels.Windows
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ICustomNavigationService _customNavigationService;
        private readonly IServiceProvider _serviceProvider;
        public OverlayToggleButtonViewModel OverlayViewModel { get; }

        [ObservableProperty]
        private string _applicationTitle = "Predator";

        [ObservableProperty]
        private ObservableCollection<object> _menuItemsSource = new();

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItemsSource = new();

        [ObservableProperty]
        private object? _dialogViewModel;

        [ObservableProperty]
        private OmnisearchViewModel? _omnisearchDialogViewModel;

        [ObservableProperty]
        private bool _isGloballyLoading;

        [ObservableProperty]
        private string? _globalLoadingMessage;

        [ObservableProperty]
        private bool _isGlobalSuccessOverlayVisible;

        [ObservableProperty]
        private string? _globalSuccessMessage;

        [ObservableProperty]
        private bool _isGlobalErrorOverlayVisible;

        [ObservableProperty]
        private string? _globalErrorMessage;

        [ObservableProperty]
        private ViewModelBase? _currentPageViewModel;

        [ObservableProperty]
        private bool _isBackButtonVisible;

        [ObservableProperty]
        private string? _currentPageTitle;

        [ObservableProperty]
        private object? _currentPageHeaderContent;

        partial void OnCurrentPageViewModelChanged(ViewModelBase? value)
        {
            IsBackButtonVisible = value is ChampionDetailPageViewModel;
        }

        public MainWindowViewModel(ICustomNavigationService customNavigationService, IServiceProvider serviceProvider, OverlayToggleButtonViewModel overlayViewModel)
        {
            _customNavigationService = customNavigationService;
            _serviceProvider = serviceProvider;
            OverlayViewModel = overlayViewModel;
            InitializeMenu();
        }

        private void InitializeMenu()
        {
            MenuItemsSource.Add(new NavigationViewItem()
            {
                Content = "Home",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(ChampionGridPage)
            });

            MenuItemsSource.Add(new NavigationViewItem()
            {
                Content = "Installed",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Save24 },
                TargetPageType = typeof(InstalledSkinsPage)
            });

            MenuItemsSource.Add(new NavigationViewItem()
            {
                Content = "Profile",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Person24 },
                TargetPageType = typeof(ProfilePage)
            });

            FooterMenuItemsSource.Add(new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(SettingsPage)
            });
        }

        [RelayCommand]
        private void GoBack()
        {
            _customNavigationService.GoBack();
        }

        public async Task ShowGlobalSuccess(string message)
        {
            GlobalSuccessMessage = message;
            IsGlobalSuccessOverlayVisible = true;
            await Task.Delay(2000);
            IsGlobalSuccessOverlayVisible = false;
        }

        public async Task ShowGlobalError(string message)
        {
            GlobalErrorMessage = message;
            IsGlobalErrorOverlayVisible = true;
            await Task.Delay(3000);
            IsGlobalErrorOverlayVisible = false;
        }

        [RelayCommand]
        private async Task OpenOmnisearch()
        {
            if (OmnisearchDialogViewModel == null)
            {
                var omnisearchVM = _serviceProvider.GetRequiredService<OmnisearchViewModel>();
                OmnisearchDialogViewModel = omnisearchVM;
                await omnisearchVM.EnsureDataLoadedAsync();
            }
        }
    }
}