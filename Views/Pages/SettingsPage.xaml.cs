using skinhunter.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace skinhunter.Views.Pages
{
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}