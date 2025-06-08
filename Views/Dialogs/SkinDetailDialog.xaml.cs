using System.Windows.Navigation;
using System.Diagnostics;

namespace skinhunter.Views.Dialogs
{
    public partial class SkinDetailDialog : System.Windows.Controls.UserControl
    {
        public SkinDetailDialog()
        {
            InitializeComponent();
        }

        private async void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open hyperlink: {ex.Message}");
                var messageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Error",
                    Content = $"Could not open link: {e.Uri.AbsoluteUri}",
                    CloseButtonText = "OK"
                };
                await messageBox.ShowDialogAsync();
            }
            e.Handled = true;
        }
    }
}