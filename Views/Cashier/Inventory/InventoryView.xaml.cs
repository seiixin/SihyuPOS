using System.Windows;
using System.Windows.Controls;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Cashier.Inventory
{
    public partial class InventoryView : UserControl
    {
        public InventoryView()
        {
            InitializeComponent();

            // Set the same VM used by admin, but we only bind to read-only bits in XAML
            DataContext = new InventoryViewModel();

            // Optional: auto-refresh on load if the VM exposes RefreshCommand
            Loaded += (s, e) =>
            {
                var vm = DataContext as InventoryViewModel;
                var cmd = vm?.RefreshCommand;
                if (cmd != null && cmd.CanExecute(null)) cmd.Execute(null);
            };
        }
    }
}