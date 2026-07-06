using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

using SihyuPOSPayroll.ViewModels;
// If you want to new-up the service explicitly here, you can alias like this:
// using IEmployeeService = SihyuPOSPayroll.Services.IEmployeeService;
// using EmployeeService  = SihyuPOSPayroll.Services.EmployeeService;

namespace SihyuPOSPayroll.Views.Employee.Profile
{
    public partial class ProfileView : UserControl
    {
        private bool _isEditing;

        // Preferred: construct with the actual logged-in employee's ID
        public ProfileView(int employeeId)
        {
            InitializeComponent();

            // Create VM with the given employeeId (VM will DI default service internally)
            var vm = new EmployeeProfileViewModel(employeeId);
            DataContext = vm;

            // Start read-only
            ToggleReadonly(true);
        }

        // Designer-only fallback (so the designer shows something)
        public ProfileView() : this(GetDesignTimeEmployeeId()) { }

        private static int GetDesignTimeEmployeeId()
        {
            // Only used by the Visual Studio designer; not at runtime navigation.
            return DesignerProperties.GetIsInDesignMode(new DependencyObject()) ? 1 : 0;
        }

        private void OnEditSaveClicked(object sender, RoutedEventArgs e)
        {
            _isEditing = !_isEditing;
            ToggleReadonly(!_isEditing);

            // When switching back to read-only, trigger save
            if (!_isEditing && DataContext is EmployeeProfileViewModel vm)
            {
                if (vm.SaveCommand?.CanExecute(null) == true)
                    vm.SaveCommand.Execute(null);
            }
        }

        private void ToggleReadonly(bool readOnly)
        {
            // Left column
            tbFullName.IsReadOnly = readOnly;
            tbAge.IsReadOnly = readOnly;
            tbSex.IsReadOnly = readOnly;
            tbAddress.IsReadOnly = readOnly;
            tbBirthday.IsReadOnly = readOnly;
            tbContact.IsReadOnly = readOnly;
            tbPosition.IsReadOnly = readOnly;
            tbSalary.IsReadOnly = readOnly;

            // Right column
            tbSSS.IsReadOnly = readOnly;
            tbPhilhealth.IsReadOnly = readOnly;
            tbPagibig.IsReadOnly = readOnly;
            tbEmail.IsReadOnly = readOnly;
            tbEmergency.IsReadOnly = readOnly;
            tbHired.IsReadOnly = readOnly;
        }
    }
}
