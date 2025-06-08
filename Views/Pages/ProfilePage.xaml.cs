using skinhunter.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace skinhunter.Views.Pages
{
    public partial class ProfilePage : INavigableView<ProfileViewModel>
    {
        public ProfileViewModel ViewModel { get; }

        public ProfilePage(ProfileViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this; // O ViewModel directamente si no usas {Binding ViewModel.Property} en XAML.
                                // Dado que tu XAML usa {Binding ViewModel.UserAvatarFallback}, y luego {Binding UserAvatarFallback},
                                // necesitas DataContext = this; y la propiedad ViewModel expuesta.
            InitializeComponent();
        }
    }
}
