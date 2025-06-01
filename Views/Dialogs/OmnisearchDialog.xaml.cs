using skinhunter.ViewModels.Dialogs;

namespace skinhunter.Views.Dialogs
{
    public partial class OmnisearchDialog : System.Windows.Controls.UserControl
    {
        public OmnisearchViewModel? ViewModel => DataContext as OmnisearchViewModel;
        public OmnisearchDialog()
        {
            InitializeComponent();
        }
    }
}