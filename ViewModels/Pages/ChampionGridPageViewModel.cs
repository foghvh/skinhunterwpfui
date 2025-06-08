using skinhunter.Models;
using skinhunter.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;

namespace skinhunter.ViewModels.Pages
{
    public partial class ChampionGridPageViewModel : ViewModelBase, INavigationAware
    {
        private readonly ICustomNavigationService _customNavigationService;
        private readonly ObservableCollection<ChampionSummary> _allChampions = new();

        [ObservableProperty]
        private string? _searchText;

        [ObservableProperty]
        private ObservableCollection<string> _allRoles = new();

        [ObservableProperty]
        private string? _selectedRole = "All";

        public ICollectionView ChampionsView { get; }

        [ObservableProperty]
        private bool _showContentWithAnimation;

        private bool _dataLoadedAtLeastOnce = false;

        public ChampionGridPageViewModel(ICustomNavigationService customNavigationService)
        {
            _customNavigationService = customNavigationService;
            ChampionsView = CollectionViewSource.GetDefaultView(_allChampions);
            ChampionsView.Filter = FilterChampions;
            AllRoles.Add("All");
        }

        public async Task OnNavigatedToAsync()
        {
            ShowContentWithAnimation = false;

            if (!_dataLoadedAtLeastOnce || !_allChampions.Any())
            {
                IsLoading = true;
                await LoadChampionsAsync();
                _dataLoadedAtLeastOnce = true;
            }
            else
            {
                IsLoading = false;
            }

            if (!IsLoading)
            {
                ShowContentWithAnimation = true;
            }
        }

        public Task OnNavigatedFromAsync()
        {
            ShowContentWithAnimation = false;
            return Task.CompletedTask;
        }

        public void OnNavigatedTo(object? parameter)
        {
        }

        public void ReleaseResourcesForTray()
        {
            IsLoading = true;
            if (_allChampions.Any())
            {
                var championsToRelease = _allChampions.ToList();
                System.Windows.Application.Current?.Dispatcher.Invoke(() => _allChampions.Clear());

                foreach (var champ in championsToRelease)
                {
                    champ.ReleaseImage();
                }
                System.Windows.Application.Current?.Dispatcher.Invoke(() => ChampionsView?.Refresh());
            }
            IsLoading = false;
        }

        partial void OnSearchTextChanged(string? value)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => ChampionsView.Refresh(), DispatcherPriority.Background);
        }

        partial void OnSelectedRoleChanged(string? value)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => ChampionsView.Refresh(), DispatcherPriority.Background);
        }

        private bool FilterChampions(object item)
        {
            if (!(item is ChampionSummary champ)) return false;

            bool textMatch = string.IsNullOrWhiteSpace(SearchText) ||
                             champ.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

            bool roleMatch = string.IsNullOrEmpty(SelectedRole) ||
                             SelectedRole.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                             (champ.Roles != null && champ.Roles.Any(r => r.Equals(SelectedRole, StringComparison.OrdinalIgnoreCase)));

            return textMatch && roleMatch;
        }

        private void PopulateRoles()
        {
            var uniqueRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_allChampions.Any())
            {
                foreach (var champ in _allChampions)
                {
                    if (champ.Roles != null)
                    {
                        foreach (var role in champ.Roles)
                        {
                            if (!string.IsNullOrWhiteSpace(role))
                            {
                                uniqueRoles.Add(role);
                            }
                        }
                    }
                }
            }

            var sortedRoles = uniqueRoles.OrderBy(r => r).ToList();

            System.Windows.Application.Current?.Dispatcher.Invoke(() => {
                string? actualCurrentSelection = SelectedRole;
                AllRoles.Clear();
                AllRoles.Add("All");
                foreach (var role in sortedRoles)
                {
                    string displayRole = role.Length > 0 ? char.ToUpper(role[0]) + role.Substring(1) : role;
                    AllRoles.Add(displayRole);
                }

                if (!string.IsNullOrEmpty(actualCurrentSelection) && AllRoles.Contains(actualCurrentSelection))
                {
                    SelectedRole = actualCurrentSelection;
                }
                else
                {
                    SelectedRole = "All";
                }
            });
        }

        [RelayCommand]
        public async Task LoadChampionsAsync()
        {
            if (!IsLoading) IsLoading = true;

            var champs = await CdragonDataService.GetChampionSummariesAsync();
            if (champs != null)
            {
                var tempChamps = champs.OrderBy(c => c.Name).ToList();
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _allChampions.Clear();
                    foreach (var champ in tempChamps)
                    {
                        if (champ.Roles == null) champ.Roles = new List<string>();
                        _allChampions.Add(champ);
                    }
                });
                PopulateRoles();
                System.Windows.Application.Current?.Dispatcher.Invoke(() => ChampionsView.Refresh());
            }
            else
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(async () => {
                    var messageBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Error",
                        Content = "Failed to load champions.",
                        CloseButtonText = "OK"
                    };
                    await messageBox.ShowDialogAsync();
                });
            }
            IsLoading = false;
        }

        [RelayCommand]
        private void SelectChampion(ChampionSummary? champion)
        {
            if (champion != null)
            {
                _customNavigationService.NavigateToChampionDetail(champion.Id);
            }
        }
    }
}