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

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open hyperlink: {ex.Message}");
                System.Windows.MessageBox.Show($"Could not open link: {e.Uri.AbsoluteUri}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            e.Handled = true;
        }
    }
}