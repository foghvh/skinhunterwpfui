/// skinhunter Start of ViewModels/OverlayToggleButtonViewModel.cs ///
using skinhunter.Services;
using Wpf.Ui.Controls;

namespace skinhunter.ViewModels
{
    public partial class OverlayToggleButtonViewModel : ObservableObject
    {
        private readonly ModToolsService _modToolsService;

        [ObservableProperty]
        private string _content = "Start Overlay";

        [ObservableProperty]
        private SymbolRegular _icon = SymbolRegular.Play24;

        public IRelayCommand ToggleOverlayCommand { get; }

        public OverlayToggleButtonViewModel(ModToolsService modToolsService)
        {
            _modToolsService = modToolsService;
            ToggleOverlayCommand = new RelayCommand(ExecuteToggleOverlay);
            _modToolsService.OverlayStatusChanged += OnOverlayStatusChanged;
            UpdateState();
        }

        private void ExecuteToggleOverlay()
        {
            if (_modToolsService.IsOverlayRunning)
            {
                _modToolsService.StopRunOverlay();
            }
            else
            {
                _modToolsService.StartRunOverlayWithInstalledSkins();
            }
        }

        private void OnOverlayStatusChanged(bool isRunning)
        {
            App.Current.Dispatcher.Invoke(UpdateState);
        }

        private void UpdateState()
        {
            if (_modToolsService.IsOverlayRunning)
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
    }
}
/// skinhunter End of ViewModels/OverlayToggleButtonViewModel.cs ///