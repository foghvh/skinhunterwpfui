using skinhunter.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls; // Para INavigableView

namespace skinhunter.Views.Pages
{
    public partial class ChampionGridPage : INavigableView<ChampionGridPageViewModel>
    {
        // Esta propiedad ViewModel es llenada por DI cuando la página se crea.
        public ChampionGridPageViewModel ViewModel { get; }

        public ChampionGridPage(ChampionGridPageViewModel viewModel)
        {
            ViewModel = viewModel; // El ViewModel inyectado se asigna a la propiedad ViewModel.
            DataContext = this;    // <<--- ¡ESTA LÍNEA ES CRUCIAL!
                                   // Establece el DataContext de la página a sí misma.
                                   // Esto permite que el XAML use bindings como {Binding ViewModel.SearchText}

            InitializeComponent();
        }
    }
}