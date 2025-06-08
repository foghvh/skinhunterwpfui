/// skinhunter Start of Views\Windows\MainWindow.xaml.cs ///
using skinhunter.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using skinhunter.Services;
using skinhunter.Views.Pages;

namespace skinhunter.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }
        private readonly IServiceProvider _serviceProvider;
        private readonly AuthTokenManager _authTokenManager;
        private readonly INavigationService _wpfUiNavigationService;

        public MainWindow(
            MainWindowViewModel viewModel,
            IServiceProvider serviceProvider,
            INavigationService navigationService,
            AuthTokenManager authTokenManager)
        {
            ViewModel = viewModel;
            DataContext = this;
            _serviceProvider = serviceProvider;
            _wpfUiNavigationService = navigationService;
            _authTokenManager = authTokenManager;

            FileLoggerService.Log("[MainWindow] Constructor called.");
            SystemThemeWatcher.Watch(this);

            InitializeComponent();

            var pageProvider = _serviceProvider.GetRequiredService<INavigationViewPageProvider>();
            SetPageService(pageProvider);

            _wpfUiNavigationService.SetNavigationControl(RootNavigation);

            SetServiceProvider(serviceProvider);

            _authTokenManager.PropertyChanged += AuthTokenManager_PropertyChanged;
            this.Loaded += MainWindow_Loaded;
            this.ContentRendered += (s, e) => FileLoggerService.Log("[MainWindow] ContentRendered event fired.");
            UpdateNavigationFrameState();
        }

        private void AuthTokenManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AuthTokenManager.IsAuthenticated))
            {
                FileLoggerService.Log($"[MainWindow] AuthTokenManager.IsAuthenticated changed to: {_authTokenManager.IsAuthenticated}. Updating nav frame state.");
                Application.Current.Dispatcher.Invoke(UpdateNavigationFrameState);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FileLoggerService.Log($"[MainWindow] MainWindow_Loaded. Current Auth: {_authTokenManager.IsAuthenticated}");
            UpdateNavigationFrameState();
        }

        private void UpdateNavigationFrameState()
        {
            FileLoggerService.Log($"[MainWindow.UpdateNavigationFrameState] IsAuthenticated: {_authTokenManager.IsAuthenticated}");
            RootNavigation.IsEnabled = true;
            FileLoggerService.Log($"[MainWindow.UpdateNavigationFrameState] RootNavigation.IsEnabled set to {RootNavigation.IsEnabled}. Menu items reflect auth state via ViewModel.");
        }

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType)
        {
            FileLoggerService.Log($"[MainWindow] Navigate request to: {pageType.Name}. Current Auth: {_authTokenManager.IsAuthenticated}");

            if (pageType == typeof(AuthenticationRequiredPage))
            {
                FileLoggerService.Log($"[MainWindow] Allowing navigation to AuthenticationRequiredPage.");
            }
            else if (!_authTokenManager.IsAuthenticated)
            {
                FileLoggerService.Log($"[MainWindow] Navigation to {pageType.Name} blocked (unauthenticated). Redirecting to AuthRequiredPage.");
                Application.Current.Dispatcher.InvokeAsync(() => _wpfUiNavigationService.Navigate(typeof(AuthenticationRequiredPage)));
                return false;
            }

            return _wpfUiNavigationService.Navigate(pageType);
        }

        private async Task NavigateAsync(Type pageType)
        {
            await Task.Yield();
            _wpfUiNavigationService.Navigate(pageType);
        }

        public void SetPageService(INavigationViewPageProvider pageProvider)
        {
            RootNavigation.SetPageProviderService(pageProvider);
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            RootNavigation.SetServiceProvider(serviceProvider);
        }

        public void ShowWindow()
        {
            FileLoggerService.Log("[MainWindow] ShowWindow() called.");
            Show();
        }

        public void CloseWindow()
        {
            FileLoggerService.Log("[MainWindow] CloseWindow() called.");
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            FileLoggerService.Log("[MainWindow] OnClosed called.");
            base.OnClosed(e);
            _authTokenManager.PropertyChanged -= AuthTokenManager_PropertyChanged;
            Application.Current.Shutdown();
        }
    }
}
/// skinhunter End of Views\Windows\MainWindow.xaml.cs ///