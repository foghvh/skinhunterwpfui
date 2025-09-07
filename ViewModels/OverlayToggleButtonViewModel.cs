// ViewModels/OverlayToggleButtonViewModel.cs
using skinhunter.Services;
using Wpf.Ui.Controls;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media.Animation;

namespace skinhunter.ViewModels
{
    public partial class OverlayToggleButtonViewModel : ViewModelBase, IDisposable
    {
        private readonly ModToolsService? _modToolsService;
        private readonly UserPreferencesService? _userPreferencesService;

        public event Action<string>? OperationStarted;
        public event Action? OperationCompleted;

        [ObservableProperty]
        private string _content = "Start Overlay";

        [ObservableProperty]
        private SymbolRegular _icon = SymbolRegular.Play24;

        [ObservableProperty]
        private bool _isOverlayBusy = false;

        [ObservableProperty]
        private bool _isUserBuyer;

        [ObservableProperty]
        private string? _statusText;

        [ObservableProperty]
        private bool _isStatusError;

        [ObservableProperty]
        private double _statusTextOpacity = 0;

        public IAsyncRelayCommand ToggleOverlayCommand { get; }

        private OverlayToggleButtonViewModel()
        {
            ToggleOverlayCommand = new AsyncRelayCommand(async () => await Task.CompletedTask, () => false);
        }

        public OverlayToggleButtonViewModel(ModToolsService modToolsService, UserPreferencesService userPreferencesService) : this()
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                _modToolsService = modToolsService;
                _userPreferencesService = userPreferencesService;
                ToggleOverlayCommand = new AsyncRelayCommand(ExecuteToggleOverlay, CanExecuteToggleOverlay);
                _modToolsService.OverlayStatusChanged += OnOverlayStatusChanged;
                _modToolsService.CommandOutputReceived += OnCommandOutputReceived;
                _userPreferencesService.PropertyChanged += UserPreferencesService_PropertyChanged;
                UpdateState();
                UpdateUserState();
            }
        }

        private void OnCommandOutputReceived(string output, bool isError)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AnimateStatusText(output, isError);
            });
        }

        private void AnimateStatusText(string newText, bool isError)
        {
            Task.Run(async () =>
            {
                await Application.Current.Dispatcher.InvokeAsync(() => { StatusTextOpacity = 0; });
                await Task.Delay(210);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusText = newText;
                    IsStatusError = isError;
                    StatusTextOpacity = 1;
                });
            });
        }


        private void UserPreferencesService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserPreferencesService.CurrentProfile))
            {
                Application.Current.Dispatcher.Invoke(UpdateUserState);
            }
        }

        private void UpdateUserState()
        {
            IsUserBuyer = _userPreferencesService?.CurrentProfile?.IsBuyer ?? false;
            ToggleOverlayCommand?.NotifyCanExecuteChanged();
        }

        public bool CanExecuteToggleOverlay()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return !IsOverlayBusy;
            return IsUserBuyer && !IsOverlayBusy;
        }

        private async Task ExecuteToggleOverlay()
        {
            if (!CanExecuteToggleOverlay() || _modToolsService is null) return;

            IsOverlayBusy = true;
            ToggleOverlayCommand.NotifyCanExecuteChanged();
            var operationMessage = _modToolsService.IsOverlayRunning ? "Stopping Overlay..." : "Starting Overlay...";
            OperationStarted?.Invoke(operationMessage);
            AnimateStatusText(operationMessage, false);

            try
            {
                if (_modToolsService.IsOverlayRunning)
                {
                    await _modToolsService.StopRunOverlayAsync();
                }
                else
                {
                    // CORRECCIÓN: Usar el nombre de método correcto.
                    await _modToolsService.QueueSyncAndRebuild();
                }
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[OverlayToggleVM] Error executing toggle overlay: {ex.Message}");
                AnimateStatusText("Operation failed.", true);
            }
            finally
            {
                IsOverlayBusy = false;
                ToggleOverlayCommand.NotifyCanExecuteChanged();
            }
        }

        private void OnOverlayStatusChanged(bool isRunning)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateState();
                AnimateStatusText(isRunning ? "Overlay is running" : "Overlay is stopped", false);
                OperationCompleted?.Invoke();

                _ = Task.Delay(4000).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (StatusText == "Overlay is running" || StatusText == "Overlay is stopped")
                        {
                            AnimateStatusText(string.Empty, false);
                        }
                    });
                });
            });
        }

        private void UpdateState()
        {
            if (_modToolsService?.IsOverlayRunning ?? false)
            {
                Content = "Stop Overlay";
                Icon = SymbolRegular.Stop24;
            }
            else
            {
                Content = "Start Overlay";
                Icon = SymbolRegular.Play24;
            }
        }

        public void Dispose()
        {
            if (_modToolsService != null)
            {
                _modToolsService.OverlayStatusChanged -= OnOverlayStatusChanged;
                _modToolsService.CommandOutputReceived -= OnCommandOutputReceived;
            }
            if (_userPreferencesService != null)
            {
                _userPreferencesService.PropertyChanged -= UserPreferencesService_PropertyChanged;
            }
            GC.SuppressFinalize(this);
        }
    }
}