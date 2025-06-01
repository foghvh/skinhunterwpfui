using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;


namespace skinhunter.ViewModels.Dialogs
{
    public partial class SkinDetailViewModel : ViewModelBase
    {
        private readonly ICustomNavigationService _customNavigationService;

        [ObservableProperty]
        private Skin? _selectedSkin;

        public ObservableCollection<Chroma> AvailableChromas { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDefaultSelected))]
        [NotifyPropertyChangedFor(nameof(KhadaViewerUrl))]
        private Chroma? _selectedChroma;

        [ObservableProperty]
        private int _userCredits = 5;

        public bool IsDefaultSelected => SelectedChroma == null;

        public string? KhadaViewerUrl
        {
            get
            {
                if (SelectedSkin == null) return null;
                int skinId = SelectedSkin.Id;
                int? chromaId = SelectedChroma?.Id;
                string url = $"https://modelviewer.lol/model-viewer?id={skinId}";
                if (chromaId.HasValue && chromaId.Value != 0 && chromaId.Value / 1000 == skinId)
                {
                    url += $"&chroma={chromaId.Value}";
                }
                return url;
            }
        }

        public SkinDetailViewModel(ICustomNavigationService customNavigationService)
        {
            _customNavigationService = customNavigationService;
        }

        public async Task LoadSkinAsync(Skin skin)
        {
            IsLoading = true;

            await CdragonDataService.EnrichSkinWithSupabaseChromaDataAsync(skin);
            SelectedSkin = skin;

            AvailableChromas.Clear();

            if (SelectedSkin.Chromas != null && SelectedSkin.Chromas.Any())
            {
                foreach (var chroma in SelectedSkin.Chromas)
                {
                    if (chroma != null)
                    {
                        chroma.IsSelected = false;
                        AvailableChromas.Add(chroma);
                    }
                }
            }
            else
            {
            }

            SelectedChroma = null;
            DownloadSkinCommand.NotifyCanExecuteChanged();
            IsLoading = false;
        }

        public bool CanDownload()
        {
            return UserCredits > 0;
        }

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private async Task DownloadSkinAsync()
        {
            IsLoading = true;
            var skinOrChromaName = IsDefaultSelected ? SelectedSkin?.Name : SelectedChroma?.Name;
            var idToDownload = IsDefaultSelected ? SelectedSkin?.Id : SelectedChroma?.Id;

            await Task.Delay(1500);

            UserCredits--;
            DownloadSkinCommand.NotifyCanExecuteChanged();

            IsLoading = false;
            System.Windows.MessageBox.Show($"'{skinOrChromaName}' (ID: {idToDownload}) download initiated!", "Download", MessageBoxButton.OK, MessageBoxImage.Information);

            CloseDialog();
        }

        [RelayCommand]
        private void CloseDialog()
        {
            _customNavigationService.CloseDialog();
        }

        private void SetDefaultSelection()
        {
            SelectedChroma = null;
            RefreshChromaSelections(null);
        }

        [RelayCommand]
        private void ToggleChromaSelection(Chroma? clickedChroma)
        {
            if (clickedChroma == null) return;

            if (SelectedChroma == clickedChroma)
            {
                SetDefaultSelection();
            }
            else
            {
                SelectedChroma = clickedChroma;
                RefreshChromaSelections(SelectedChroma);
            }
            DownloadSkinCommand.NotifyCanExecuteChanged();
        }

        private void RefreshChromaSelections(Chroma? selected)
        {
            foreach (var ch in AvailableChromas)
            {
                ch.IsSelected = (ch == selected);
            }
        }
    }
}