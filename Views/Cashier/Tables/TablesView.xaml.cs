using System.Windows.Controls;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Cashier.Tables
{
    public partial class TablesView : UserControl
    {
        public TablesView()
        {
            InitializeComponent();
            DataContext = new TableViewModel(); // reuse your VM
        }
    }
}
