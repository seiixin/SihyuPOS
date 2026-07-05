using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SihyuPOSPayroll.Views.Admin.Users
{
    public partial class AddEditUser : UserControl
    {
        private readonly UserService _userService = new();
        private readonly EmployeeService _EmployeeService = new();

        private readonly bool _isEditMode;
        private readonly UserModel? _editingUser;

        public List<string> Roles { get; set; } = new() { "Admin", "Cashier", "Employee" };
        public List<EmployeeModel> Employees { get; set; } = new();

        // Event to notify parent when saved
        public delegate void UserSavedHandler();
        public event UserSavedHandler? OnUserSaved;

        public AddEditUser(UserModel? user = null)
        {
            InitializeComponent();

            DataContext = this;

            // Load employees for linking
            Employees = _EmployeeService.GetAllEmployees();
            EmployeeComboBox.ItemsSource = Employees;

            if (user != null)
            {
                _isEditMode = true;
                _editingUser = user;

                TitleText.Text = "Edit User";
                EmailTextBox.Text = user.Email;
                PasswordBox.Password = user.Password;
                RoleComboBox.SelectedItem = user.Role;
                if (user.EmployeeId.HasValue)
                    EmployeeComboBox.SelectedValue = user.EmployeeId.Value;
            }
            else
            {
                _isEditMode = false;
                TitleText.Text = "Add New User";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Remove or hide this UserControl
            var parent = this.Parent as Panel;
            parent?.Children.Remove(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var role = RoleComboBox.SelectedItem?.ToString();
            var employeeId = EmployeeComboBox.SelectedValue as int?;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(role))
            {
                MessageBox.Show("Please fill required fields.");
                return;
            }

            if (_isEditMode && _editingUser != null)
            {
                // Update existing user
                var updatedUser = new UserModel
                {
                    Id = _editingUser.Id,
                    Email = email,
                    Password = password,
                    Role = role,
                    EmployeeId = employeeId
                };

                bool success = _userService.UpdateUser(updatedUser);

                if (success)
                {
                    MessageBox.Show("User updated successfully.");
                    OnUserSaved?.Invoke();
                    Cancel_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Failed to update user.");
                }
            }
            else
            {
                // Add new user
                var newUser = new UserModel
                {
                    Email = email,
                    Password = password,
                    Role = role,
                    EmployeeId = employeeId
                };

                bool success = _userService.AddUser(newUser);

                if (success)
                {
                    MessageBox.Show("User added successfully.");
                    OnUserSaved?.Invoke();
                    Cancel_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Failed to add user.");
                }
            }
        }
    }
}
