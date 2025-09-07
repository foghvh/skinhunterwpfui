using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.IO;
using System.Collections.Generic;
using Microsoft.Win32;

namespace skinhunter.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private readonly UserPreferencesService _userPreferencesService;
        private readonly MainWindowViewModel _mainWindowViewModel;
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private bool _isDarkTheme;

        [ObservableProperty]
        private bool _syncOnStart;

        [ObservableProperty]
        private string? _gamePath;

        public ObservableCollection<Color> PredefinedAccentColors { get; }
        public ObservableCollection<WindowBackdropType> AvailableBackdropTypes { get; }

        [ObservableProperty]
        private WindowBackdropType _selectedBackdropType;

        public SettingsViewModel(UserPreferencesService userPreferencesService, MainWindowViewModel mainWindowViewModel)
        {
            _userPreferencesService = userPreferencesService;
            _mainWindowViewModel = mainWindowViewModel;

            PredefinedAccentColors = new ObservableCollection<Color>
            {
                Color.FromRgb(0, 120, 215),
                Color.FromRgb(231, 76, 60),
                Color.FromRgb(46, 204, 113),
                Color.FromRgb(155, 89, 182),
                Color.FromRgb(241, 196, 15),
                Color.FromRgb(26, 188, 156),
                Color.FromRgb(230, 126, 34),
                Color.FromRgb(255, 105, 180)
            };

            AvailableBackdropTypes = new ObservableCollection<WindowBackdropType>
            {
                WindowBackdropType.Auto,
                WindowBackdropType.Mica,
                WindowBackdropType.Acrylic,
                WindowBackdropType.Tabbed,
                WindowBackdropType.None
            };
        }

        partial void OnIsDarkThemeChanged(bool value)
        {
            if (!_isInitialized) return;
            ApplicationThemeManager.Apply(value ? ApplicationTheme.Dark : ApplicationTheme.Light);
        }

        partial void OnSelectedBackdropTypeChanged(WindowBackdropType value)
        {
            if (!_isInitialized) return;
            if (Application.Current.MainWindow is Views.Windows.MainWindow mainWindow)
            {
                mainWindow.WindowBackdropType = value;
            }
        }

        [RelayCommand]
        private void ChangeAccentColor(object? color)
        {
            if (color is Color accentColor)
            {
                ApplicationAccentColorManager.Apply(accentColor, ApplicationThemeManager.GetAppTheme());
            }
        }

        [RelayCommand]
        private void OpenModsFolder()
        {
            try
            {
                string appExePath = Path.GetDirectoryName(AppContext.BaseDirectory) ?? ".";
                string modsPath = Path.Combine(appExePath, "UserData", "LoLModInstaller", "installed");
                Directory.CreateDirectory(modsPath);
                Process.Start("explorer.exe", modsPath);
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[SettingsVM] Failed to open mods folder: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SelectGamePath()
        {
            var openFolderDialog = new OpenFolderDialog
            {
                Title = "Select League of Legends 'Game' Folder",
                InitialDirectory = !string.IsNullOrEmpty(GamePath) && Directory.Exists(GamePath) ? GamePath : @"C:\",
            };

            if (openFolderDialog.ShowDialog() == true)
            {
                GamePath = openFolderDialog.FolderName;
            }
        }

        public async Task OnNavigatedToAsync()
        {
            _mainWindowViewModel.CurrentPageTitle = "Settings";
            _mainWindowViewModel.CurrentPageHeaderContent = null;
            if (!_isInitialized)
                await InitializeViewModel();
        }

        public Task OnNavigatedFromAsync()
        {
            return SaveSettings();
        }

        public void OnNavigatedTo(object? parameter) { }

        private async Task InitializeViewModel()
        {
            await _userPreferencesService.LoadPreferencesAsync();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsDarkTheme = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
            });
            AppVersion = $"skinhunter - {GetAssemblyVersion()}";
            SyncOnStart = _userPreferencesService.GetSyncOnStart();
            GamePath = _userPreferencesService.GetGamePath();
            SelectedBackdropType = _userPreferencesService.GetBackdropType();

            _isInitialized = true;
        }

        private async Task SaveSettings()
        {
            if (!_isInitialized) return;

            var currentPrefs = new UserPreferences
            {
                Theme = IsDarkTheme ? "dark" : "light",
                SyncOnStart = this.SyncOnStart,
                GamePath = this.GamePath,
                BackdropType = this.SelectedBackdropType.ToString(),
                InstalledSkins = _userPreferencesService.GetInstalledSkins()
            };
            await _userPreferencesService.SavePreferencesAsync(currentPrefs);
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? String.Empty;
        }
    }
}