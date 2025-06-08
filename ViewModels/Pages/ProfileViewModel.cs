
using skinhunter.Services;
using System.Security.Claims;
using System.Threading.Tasks;
using Wpf.Ui.Abstractions.Controls;

namespace skinhunter.ViewModels.Pages
{
    public partial class ProfileViewModel : ViewModelBase, INavigationAware
    {
        private readonly UserPreferencesService _userPreferencesService;

        [ObservableProperty]
        private string? _userName;

        [ObservableProperty]
        private string? _userAvatarFallback;

        [ObservableProperty]
        private int _userCredits;


        public ProfileViewModel(UserPreferencesService userPreferencesService)
        {
            _userPreferencesService = userPreferencesService;
        }

        public Task OnNavigatedToAsync()
        {
            IsLoading = true;
            var profile = _userPreferencesService.CurrentProfile;

            if (profile != null)
            {
                UserName = profile.Login ?? profile.Id.ToString();
                UserAvatarFallback = !string.IsNullOrEmpty(UserName) && UserName != "N/A" ? UserName[0].ToString().ToUpper() : "?";
                UserCredits = 0;
            }
            else
            {
                UserName = "Not Authenticated";
                UserAvatarFallback = "!";
                UserCredits = 0;
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
