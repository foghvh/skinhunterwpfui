using skinhunter.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace skinhunter.Views.Pages
{
    public partial class InstalledSkinsPage : INavigableView<InstalledSkinsViewModel>
    {
        public InstalledSkinsViewModel ViewModel { get; }

        public InstalledSkinsPage(InstalledSkinsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}