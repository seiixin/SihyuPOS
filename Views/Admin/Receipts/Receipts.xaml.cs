using System.Windows;
using System.Windows.Controls;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Admin.Receipts
{
    public partial class Receipts : UserControl
    {
        public Receipts()
        {
            InitializeComponent();
            // Set VM in code-behind to avoid XAML CLR-type resolution issues during partial builds
            DataContext = new ReceiptsViewModel();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }
    }
}
