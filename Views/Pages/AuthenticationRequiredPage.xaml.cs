using skinhunter.ViewModels.Pages;

namespace skinhunter.Views.Pages
{
    public partial class AuthenticationRequiredPage : INavigableView<AuthenticationRequiredPageViewModel>
    {
        public AuthenticationRequiredPageViewModel ViewModel { get; }

        public AuthenticationRequiredPage(AuthenticationRequiredPageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}