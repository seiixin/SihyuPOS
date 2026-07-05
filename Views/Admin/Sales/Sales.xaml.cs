using System.Windows.Controls;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Admin.Sales
{
    public partial class Sales : UserControl
    {
        public Sales()
        {
            InitializeComponent();
            DataContext = new SalesViewModel();
        }
    }
}
