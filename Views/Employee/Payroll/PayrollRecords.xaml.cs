using System.Windows.Controls;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Employee.Payroll
{
    public partial class PayrollRecords : UserControl
    {
        public PayrollRecords()
        {
            InitializeComponent();
            DataContext = new EmployeePayrollRecordsViewModel();
        }

        // ? Overload used by Sidebar: new PayrollRecords(_employeeId)
        public PayrollRecords(int employeeId) : this()
        {
            if (DataContext is EmployeePayrollRecordsViewModel vm)
                vm.SetEmployee(employeeId);
        }
    }
}
