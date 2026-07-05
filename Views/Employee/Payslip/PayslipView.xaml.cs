using System.Windows;
using System.Windows.Controls;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Employee.Payslip
{
    public partial class PayslipView : UserControl
    {
        public PayslipView()
        {
            InitializeComponent();
        }

        // ===== Dependency props so parent can pass logged-in user info =====
        public int EmployeeId
        {
            get => (int)GetValue(EmployeeIdProperty);
            set => SetValue(EmployeeIdProperty, value);
        }
        public static readonly DependencyProperty EmployeeIdProperty =
            DependencyProperty.Register(nameof(EmployeeId), typeof(int), typeof(PayslipView),
                new PropertyMetadata(1, OnEmployeeInfoChanged));

        public string EmployeeFullName
        {
            get => (string)GetValue(EmployeeFullNameProperty);
            set => SetValue(EmployeeFullNameProperty, value);
        }
        public static readonly DependencyProperty EmployeeFullNameProperty =
            DependencyProperty.Register(nameof(EmployeeFullName), typeof(string), typeof(PayslipView),
                new PropertyMetadata(string.Empty, OnEmployeeInfoChanged));

        private static void OnEmployeeInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (PayslipView)d;
            if (view.DataContext is EmployeePayslipViewModel vm)
            {
                vm.EmployeeId = view.EmployeeId;
                vm.EmployeeFullName = view.EmployeeFullName ?? string.Empty;
            }
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is EmployeePayslipViewModel vm)
            {
                // Make sure VM knows the current user
                vm.EmployeeId = EmployeeId <= 0 ? vm.EmployeeId : EmployeeId;
                vm.EmployeeFullName = string.IsNullOrWhiteSpace(EmployeeFullName) ? vm.EmployeeFullName : EmployeeFullName;

                // Use the new VM refresh (loads payslips + requests)
                // If you prefer the old call, you can keep vm.LoadPayslipsFromDatabase();
                await vm.RefreshAsync();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is EmployeePayslipViewModel vm)
            {
                vm.FilterPayslips(((TextBox)sender).Text);
            }
        }
    }
}