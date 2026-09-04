using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            => await Vm.LoadDataAsync();

        // Redirect wheel events from the DataGrid's own internal ScrollViewer
        // up to our outer MinimalistScrollViewer so the thin thumb actually moves.
        private void RecentOrdersGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source      = sender,
                };
                RecentOrdersScroller.RaiseEvent(args);
            }
        }

        private void MonthlyPill_Click(object sender, MouseButtonEventArgs e)
            => Vm.IsMonthlyMode = true;

        private void YearlyPill_Click(object sender, MouseButtonEventArgs e)
            => Vm.IsYearlyMode = true;
    }
}
