using skinhunter.ViewModels.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions; // Para INavigationViewPageProvider e INavigationWindow
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace skinhunter.Views.Windows
{
    public partial class MainWindow : INavigationWindow // Asegurarse que : INavigationWindow está aquí
    {
        public MainWindowViewModel ViewModel { get; }
        private readonly IServiceProvider _serviceProvider; // Para el método SetServiceProvider

        public MainWindow(
            MainWindowViewModel viewModel,
            IServiceProvider serviceProvider,
            INavigationService navigationService // El INavigationService global
        )
        {
            ViewModel = viewModel;
            DataContext = this;
            _serviceProvider = serviceProvider; // Guardar para SetServiceProvider si fuera necesario directamente por WPF-UI internamente

            SystemThemeWatcher.Watch(this);

            InitializeComponent();

            // Configurar el INavigationView con el PageService (INavigationViewPageProvider)
            var pageProvider = _serviceProvider.GetRequiredService<INavigationViewPageProvider>();
            SetPageService(pageProvider); // Este método es de INavigationWindow

            // Configurar el servicio de navegación con el control de navegación de esta ventana
            navigationService.SetNavigationControl(RootNavigation);

            // El método SetServiceProvider de la INTERFAZ INavigationWindow.
            // Este método es para un caso de uso donde el INavigationViewPageProvider
            // es creado sin conocimiento del IServiceProvider y necesita ser inyectado después.
            // Dado que AddNavigationViewPageProvider() ya lo configura con DI,
            // esta llamada podría no ser estrictamente necesaria para *nuestro* código,
            // pero es parte de la interfaz y algunas lógicas internas de WPF-UI podrían esperarlo.
            SetServiceProvider(serviceProvider);
        }

        #region INavigationWindow methods

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        // Implementación explícita o implícita de SetPageService
        public void SetPageService(INavigationViewPageProvider pageProvider)
        {
            RootNavigation.SetPageProviderService(pageProvider);
        }

        // Implementación explícita o implícita de SetServiceProvider
        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            // Aunque nuestro INavigationViewPageProvider ya está configurado con DI,
            // la interfaz INavigationWindow lo requiere.
            // Aquí podríamos hacer algo si fuera necesario, o simplemente cumplir la interfaz.
            // El RootNavigation.SetServiceProvider() podría ser relevante si INavigationView lo tuviera.
            // Por ahora, cumplimos la interfaz. Si el INavigationView necesita el provider directamente,
            // ya se lo pasamos con SetPageProviderService.
            RootNavigation.SetServiceProvider(serviceProvider); // NavigationView también tiene este método.
        }

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }
}