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
    }
}
