namespace skinhunter.ViewModels.Pages
{
    public partial class AuthenticationRequiredPageViewModel : ViewModelBase, INavigationAware
    {
        [ObservableProperty]
        private string _message = "Authentication is required to use Skin Hunter.";

        [ObservableProperty]
        private string _instruction = "Please launch Skin Hunter through the SHLauncher application after logging in.";

        public AuthenticationRequiredPageViewModel()
        {
        }

        public Task OnNavigatedToAsync()
        {
            IsLoading = false;
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            return Task.CompletedTask;
        }

        public void OnNavigatedTo(object? parameter)
        {
        }

        [RelayCommand]
        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }
    }
}