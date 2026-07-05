using System.Windows.Controls;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Employee.Attendance
{
    public partial class AttendanceView : UserControl
    {
        // Default (for XAML designer and cases where no employee id is passed)
        public AttendanceView()
        {
            InitializeComponent();
            DataContext = new AttendanceEmployeeViewModel();
        }

        // NEW overload: allows passing employeeId directly (used by SidebarViewModel)
        public AttendanceView(int employeeId)
        {
            InitializeComponent();
            DataContext = new AttendanceEmployeeViewModel(employeeId);
        }
    }
}
