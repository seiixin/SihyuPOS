using System.Windows;
using System.Windows.Controls;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Admin.Dashboard
{
    public partial class Dashboard : UserControl
    {
        private DashboardViewModel Vm => (DashboardViewModel)DataContext;

        public Dashboard()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
            Loaded += async (_, _) => await Vm.LoadDataAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await Vm.LoadDataAsync();
        }
    }
}
