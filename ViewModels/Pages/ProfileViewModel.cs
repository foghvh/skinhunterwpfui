// ViewModels/Pages/ProfileViewModel.cs
using skinhunter.Services;
using System.Security.Claims;
using System.Threading.Tasks;
using Wpf.Ui.Abstractions.Controls;

namespace skinhunter.ViewModels.Pages
{
    public partial class ProfileViewModel : ViewModelBase, INavigationAware
    {
        private readonly UserPreferencesService _userPreferencesService;
        private readonly MainWindowViewModel _mainWindowViewModel;

        [ObservableProperty]
        private string? _userName;

        [ObservableProperty]
        private string? _userAvatarFallback;

        [ObservableProperty]
        private string? _licenseStatus;

        [ObservableProperty]
        private bool? _isBuyer;

        public ProfileViewModel(UserPreferencesService userPreferencesService, MainWindowViewModel mainWindowViewModel)
        {
            _userPreferencesService = userPreferencesService;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public Task OnNavigatedToAsync()
        {
            _mainWindowViewModel.CurrentPageTitle = "Profile";
            _mainWindowViewModel.CurrentPageHeaderContent = null;
            IsLoading = true;
            var profile = _userPreferencesService.CurrentProfile;

            if (profile != null)
            {
                UserName = profile.Login ?? profile.Id.ToString();
                UserAvatarFallback = !string.IsNullOrEmpty(UserName) && UserName != "N/A" ? UserName[0].ToString().ToUpper() : "?";
                IsBuyer = profile.IsBuyer;
                LicenseStatus = profile.IsBuyer ? "Buyer" : "Standard User";
            }
            else
            {
                UserName = "Not Authenticated";
                UserAvatarFallback = "!";
                IsBuyer = null;
                LicenseStatus = "N/A";
            }
            FileLoggerService.Log($"[ProfileVM] Loaded. UserName from Profile Service: {UserName}");
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
    }
}