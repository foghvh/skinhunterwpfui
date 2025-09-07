using skinhunter.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using skinhunter.Services;
using skinhunter.Views.Pages;
using Wpf.Ui.Controls;
using System;
using System.Windows;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Abstractions.Controls;
using System.Linq;
using Wpf.Ui.Animations;
using Wpf.Ui.Appearance;

namespace skinhunter.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }
        private readonly UserPreferencesService _userPreferencesService;

        public MainWindow(
            MainWindowViewModel viewModel,
            IServiceProvider serviceProvider,
            INavigationService navigationService,
            ISnackbarService snackbarService,
            INavigationViewPageProvider pageProvider)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            _userPreferencesService = serviceProvider.GetRequiredService<UserPreferencesService>();

            SystemThemeWatcher.Watch(this);

            InitializeComponent();

            ApplyBackdropPreference();
            _userPreferencesService.PreferencesChanged += OnPreferencesChanged;

            navigationService.SetNavigationControl(RootNavigation);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);
            SetPageService(pageProvider);
            SetServiceProvider(serviceProvider);

            RootNavigation.Navigated += OnNavigated;

            var authTokenManager = serviceProvider.GetRequiredService<AuthTokenManager>();
            this.Loaded += (s, e) => {
                if (!authTokenManager.IsAuthenticated)
                {
                    navigationService.Navigate(typeof(AuthenticationRequiredPage));
                }
                else
                {
                    navigationService.Navigate(typeof(ChampionGridPage));
                }
            };
        }

        private void OnPreferencesChanged()
        {
            Dispatcher.Invoke(ApplyBackdropPreference);
        }

        private void ApplyBackdropPreference()
        {
            this.WindowBackdropType = _userPreferencesService.GetBackdropType();
        }

        private void OnNavigated(object sender, NavigatedEventArgs e)
        {
            if (e.Page is FrameworkElement { DataContext: ViewModelBase viewModel })
            {
                ViewModel.CurrentPageViewModel = viewModel;
            }

            // The title is now set by each page's ViewModel in its OnNavigatedToAsync method.
            // This method is now only responsible for updating the current ViewModel and applying animations.

            TransitionAnimationProvider.ApplyTransition(e.Page, Transition.FadeInWithSlide, 300);
        }

        public INavigationView GetNavigation() => RootNavigation;
        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
        public void SetPageService(INavigationViewPageProvider pageProvider) => RootNavigation.SetPageProviderService(pageProvider);
        public void SetServiceProvider(IServiceProvider serviceProvider) => RootNavigation.SetServiceProvider(serviceProvider);
        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        protected override void OnClosed(EventArgs e)
        {
            _userPreferencesService.PreferencesChanged -= OnPreferencesChanged;
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }
}