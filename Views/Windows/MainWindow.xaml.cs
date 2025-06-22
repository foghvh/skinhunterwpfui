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

namespace skinhunter.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        public MainWindow(
            MainWindowViewModel viewModel,
            IServiceProvider serviceProvider,
            INavigationService navigationService,
            INavigationViewPageProvider pageProvider)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();

            navigationService.SetNavigationControl(RootNavigation);
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

        private void OnNavigated(object sender, NavigatedEventArgs e)
        {
            if (e.Page is FrameworkElement { DataContext: ViewModelBase viewModel } page)
            {
                ViewModel.CurrentPageViewModel = viewModel;

                var navigationView = sender as INavigationView;
                var selectedItem = navigationView?.MenuItems.OfType<INavigationViewItem>().FirstOrDefault(i => i.TargetPageType == page.GetType())
                                ?? navigationView?.FooterMenuItems.OfType<INavigationViewItem>().FirstOrDefault(i => i.TargetPageType == page.GetType());

                if (selectedItem != null)
                {
                    ViewModel.CurrentPageTitle = selectedItem.Content as string;
                }
                else if (page.GetType() != typeof(ChampionDetailPage))
                {
                    ViewModel.CurrentPageTitle = "Page";
                }
            }
        }

        public INavigationView GetNavigation() => RootNavigation;
        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
        public void SetPageService(INavigationViewPageProvider pageProvider) => RootNavigation.SetPageProviderService(pageProvider);
        public void SetServiceProvider(IServiceProvider serviceProvider) => RootNavigation.SetServiceProvider(serviceProvider);
        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }
}