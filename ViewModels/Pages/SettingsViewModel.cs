using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace skinhunter.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            return Task.CompletedTask;
        }

        public void OnNavigatedTo(object? parameter)
        {
            // No parameter expected for this page currently
        }

        private void InitializeViewModel()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"skinhunter - {GetAssemblyVersion()}";
            _isInitialized = true;
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        [RelayCommand]
        private void ChangeTheme(string parameter)
        {
            ApplicationTheme newTheme;
            switch (parameter.ToLowerInvariant())
            {
                case "theme_light":
                    newTheme = ApplicationTheme.Light;
                    break;
                case "theme_dark":
                    newTheme = ApplicationTheme.Dark;
                    break;
                default:
                    return;
            }

            if (CurrentTheme == newTheme)
                return;

            ApplicationThemeManager.Apply(newTheme);
            CurrentTheme = newTheme;
        }
    }
}