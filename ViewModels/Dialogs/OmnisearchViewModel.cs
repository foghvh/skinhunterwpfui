using skinhunter.Models;
using skinhunter.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;


namespace skinhunter.ViewModels.Dialogs
{
    public partial class OmnisearchViewModel : ViewModelBase
    {
        private readonly ICustomNavigationService? _customNavigationService;
        private readonly IServiceProvider? _serviceProvider;
        private List<ChampionSummary> _allChampionsMasterList = [];
        private List<Skin> _allSkinsMasterList = [];
        private Dictionary<int, ChampionSummary> _championMap = [];

        [ObservableProperty]
        private string? _query;

        [ObservableProperty]
        private bool _showChampionsFilter = true;

        [ObservableProperty]
        private bool _showSkinsFilter = true;

        [ObservableProperty]
        private bool _isFilterPopupOpen;

        [ObservableProperty]
        private bool _isLoadingSearchResults;


        public ObservableCollection<SearchResultItem> SearchResults { get; } = [];
        public ICollectionView SearchResultsView { get; }

        public OmnisearchViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                Query = "Search...";
            }
            _customNavigationService = null;
            _serviceProvider = null;
            SearchResultsView = CollectionViewSource.GetDefaultView(SearchResults);
            if (SearchResultsView.GroupDescriptions is not null)
            {
                SearchResultsView.GroupDescriptions.Add(new PropertyGroupDescription("DisplayType"));
            }
        }

        public OmnisearchViewModel(ICustomNavigationService customNavigationService, IServiceProvider serviceProvider)
        {
            _customNavigationService = customNavigationService ?? throw new ArgumentNullException(nameof(customNavigationService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            SearchResultsView = CollectionViewSource.GetDefaultView(SearchResults);
            if (SearchResultsView.GroupDescriptions is not null)
            {
                SearchResultsView.GroupDescriptions.Add(new PropertyGroupDescription("DisplayType"));
            }
        }

        private bool _isDataLoaded = false;

        public async Task EnsureDataLoadedAsync()
        {
            if (_isDataLoaded) return;
            if (_serviceProvider is null) return;

            IsLoading = true;
            try
            {
                var champsTask = CdragonDataService.GetChampionSummariesAsync();
                var skinsTask = CdragonDataService.GetAllSkinsAsync();
                await Task.WhenAll(champsTask, skinsTask);

                var champs = await champsTask;
                if (champs is not null)
                {
                    _allChampionsMasterList = champs;
                    _championMap = champs.ToDictionary(c => c.Id);
                }

                var skinsDict = await skinsTask;
                if (skinsDict is not null)
                {
                    _allSkinsMasterList = skinsDict.Values
                        .Where(s => {
                            bool isBaseSkinName = false;
                            if (_championMap.TryGetValue(s.ChampionId, out var parentChamp))
                            {
                                isBaseSkinName = s.Name.Equals(parentChamp.Name, StringComparison.OrdinalIgnoreCase) ||
                                                 s.Name.Equals($"Base {parentChamp.Name}", StringComparison.OrdinalIgnoreCase);
                            }
                            return !isBaseSkinName && !s.Name.Contains("Original", StringComparison.OrdinalIgnoreCase);
                        })
                        .OrderBy(s => s.Name)
                        .ToList();
                }
                _isDataLoaded = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OmnisearchViewModel] Error cargando datos maestros: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnQueryChanged(string? value)
        {
            PerformSearch();
        }

        partial void OnShowChampionsFilterChanged(bool value)
        {
            PerformSearch();
        }

        partial void OnShowSkinsFilterChanged(bool value)
        {
            PerformSearch();
        }

        private CancellationTokenSource _searchCts = new();

        private async void PerformSearch()
        {
            _searchCts.Cancel();
            _searchCts = new();
            var token = _searchCts.Token;
            var currentQuery = Query;

            if (string.IsNullOrWhiteSpace(currentQuery) || currentQuery.Length < 1)
            {
                SearchResults.Clear();
                IsLoadingSearchResults = false;
                return;
            }

            if (!_isDataLoaded && _serviceProvider is not null)
            {
                IsLoadingSearchResults = true;
                await EnsureDataLoadedAsync();
                if (token.IsCancellationRequested || !_isDataLoaded)
                {
                    IsLoadingSearchResults = false;
                    SearchResults.Clear();
                    return;
                }
            }
            else if (!_isDataLoaded && _serviceProvider is null)
            {
                return;
            }

            SearchResults.Clear();
            IsLoadingSearchResults = true;

            try
            {
                List<SearchResultItem> newRawResults = await Task.Run(() => {
                    if (token.IsCancellationRequested) return [];

                    List<SearchResultItem> filteredResults = [];
                    if (ShowChampionsFilter)
                    {
                        filteredResults.AddRange(_allChampionsMasterList
                            .Where(c => c.Name.Contains(currentQuery, StringComparison.OrdinalIgnoreCase))
                            .Select(c => new SearchResultItem(c)));
                    }
                    if (ShowSkinsFilter)
                    {
                        filteredResults.AddRange(_allSkinsMasterList
                            .Where(s => s.Name.Contains(currentQuery, StringComparison.OrdinalIgnoreCase))
                            .Select(s => new SearchResultItem(s, _championMap.TryGetValue(s.ChampionId, out var champ) ? champ : null)));
                    }
                    return filteredResults.OrderBy(r => r.Type).ThenBy(r => r.Name).Take(25).ToList();
                }, token);

                if (token.IsCancellationRequested)
                {
                    IsLoadingSearchResults = false;
                    return;
                }

                foreach (var item in newRawResults)
                {
                    SearchResults.Add(item);
                }
                if (SearchResults.Any())
                {
                    _ = Task.Run(async () =>
                    {
                        var imageLoadTasks = SearchResults.Select(item => item.LoadImageAsync()).ToList();
                        try
                        {
                            await Task.WhenAll(imageLoadTasks);
                        }
                        catch (Exception imgEx)
                        {
                            Debug.WriteLine($"[OmnisearchViewModel] Error durante carga de imágenes en lote: {imgEx.Message}");
                        }
                    }, token);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex) { Debug.WriteLine($"[OmnisearchViewModel] Error durante la búsqueda: {ex.Message}"); }
            finally
            {
                IsLoadingSearchResults = false;
            }
        }

        [RelayCommand]
        private void SelectResult(SearchResultItem? selectedItem)
        {
            if (selectedItem is null || _customNavigationService is null) return;
            CloseOmnisearchDialog();
            if (selectedItem.Type == SearchResultType.Champion)
            {
                _customNavigationService.NavigateToChampionDetail(selectedItem.ChampionId);
            }
            else if (selectedItem.Type == SearchResultType.Skin && selectedItem.OriginalSkinObject is not null)
            {
                _customNavigationService.ShowSkinDetailDialog(selectedItem.OriginalSkinObject);
            }
        }

        [RelayCommand]
        private void CloseOmnisearchDialog()
        {
            IsFilterPopupOpen = false;
            _customNavigationService?.CloseOmnisearchDialog();
        }

        [RelayCommand]
        private void ToggleFilterPopup()
        {
            IsFilterPopupOpen = !IsFilterPopupOpen;
        }
    }
}