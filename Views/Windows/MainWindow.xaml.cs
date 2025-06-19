using skinhunter.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using skinhunter.Services;
using skinhunter.Views.Pages;
using Wpf.Ui.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using skinhunter.ViewModels.Pages;
using Wpf.Ui.Abstractions;
using System.Collections.Specialized;

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
            DataContext = this;

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
            if (e.Page is FrameworkElement element)
            {
                ViewModel.SetCurrentPage(element.GetType());
            }

            if (e.Page is ChampionGridPage)
            {
                var championGridVM = App.Services.GetRequiredService<ChampionGridPageViewModel>();
                var headerGrid = new Grid { Margin = new Thickness(28, 12, 28, 0) };
                headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var titleBlock = new System.Windows.Controls.TextBlock { Text = "Champions", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 15) };
                headerGrid.Children.Add(titleBlock);
                Grid.SetRow(titleBlock, 0);

                var searchPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };

                var searchBox = new Wpf.Ui.Controls.TextBox { PlaceholderText = "Search Champions...", MinWidth = 250, Margin = new Thickness(0, 0, 15, 0), Height = 36, VerticalContentAlignment = VerticalAlignment.Center };
                searchBox.Icon = new SymbolIcon(SymbolRegular.Search24);
                searchBox.SetBinding(Wpf.Ui.Controls.TextBox.TextProperty, new Binding("SearchText") { Source = championGridVM, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                searchPanel.Children.Add(searchBox);

                var roleLabel = new System.Windows.Controls.TextBlock { Text = "Role:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 8, 0) };
                searchPanel.Children.Add(roleLabel);

                var roleComboBox = new ComboBox { MinWidth = 150, Height = 36, VerticalAlignment = VerticalAlignment.Center };
                roleComboBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("AllRoles") { Source = championGridVM });
                roleComboBox.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, new Binding("SelectedRole") { Source = championGridVM, Mode = BindingMode.TwoWay });
                searchPanel.Children.Add(roleComboBox);

                headerGrid.Children.Add(searchPanel);
                Grid.SetRow(searchPanel, 1);

                RootNavigation.Header = headerGrid;
            }
            else
            {
                RootNavigation.Header = null;
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