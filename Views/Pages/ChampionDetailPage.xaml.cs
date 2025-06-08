using skinhunter.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace skinhunter.Views.Pages
{
    public partial class ChampionDetailPage : INavigableView<ChampionDetailPageViewModel>
    {
        public ChampionDetailPageViewModel ViewModel { get; }

        public ChampionDetailPage(ChampionDetailPageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}