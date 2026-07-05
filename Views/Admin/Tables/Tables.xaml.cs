using System.Windows.Controls;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Admin.Tables
{
    public partial class Tables : UserControl
    {
        public Tables()
        {
            InitializeComponent();
            DataContext = new TableViewModel(); // Admin uses TableViewModel
        }
    }
}
