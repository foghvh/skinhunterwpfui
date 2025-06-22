using skinhunter.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace skinhunter.Views.Pages
{
    public partial class ChampionGridPage : INavigableView<ChampionGridPageViewModel>
    {
        public ChampionGridPageViewModel ViewModel { get; }

        public ChampionGridPage(ChampionGridPageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}