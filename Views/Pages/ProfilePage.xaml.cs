// Views/Pages/ProfilePage.xaml.cs
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
            DataContext = this;
            InitializeComponent();
        }
    }
}