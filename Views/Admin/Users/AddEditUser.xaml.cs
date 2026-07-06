#nullable enable
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SihyuPOSPayroll.Views.Admin.Users
{
    public partial class AddEditUser : UserControl
    {
        private readonly UserService _userService = new();
        private readonly EmployeeService _employeeService = new();

        private readonly bool _isEditMode;
        private readonly UserModel? _editingUser;

        public delegate void UserSavedHandler();
        public event UserSavedHandler? OnUserSaved;

        public AddEditUser(UserModel? user = null)
        {
            InitializeComponent();
            PopulateRoles();
            PopulateEmployees();

            if (user != null)
            {
                _isEditMode = true;
                _editingUser = user;
                TitleText.Text = "Edit User";
                EmailTextBox.Text = user.Email ?? string.Empty;
                PasswordBox.Password = user.Password ?? string.Empty;
                RoleComboBox.Text = user.Role ?? string.Empty;
                if (user.EmployeeId.HasValue)
                    EmployeeComboBox.SelectedValue = user.EmployeeId.Value;
            }
            else
            {
                _isEditMode = false;
                TitleText.Text = "Add New User";
            }
        }

        private void PopulateRoles()
        {
            RoleComboBox.ItemsSource = new List<string> { "Admin", "Employee", "Cashier" };
        }

        private void PopulateEmployees()
        {
            try
            {
                EmployeeComboBox.ItemsSource = _employeeService.GetAllEmployees();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to load employees: " + ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Parent is Panel parent)
                parent.Children.Remove(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EmailTextBox.Text) || string.IsNullOrWhiteSpace(RoleComboBox.Text))
            {
                MessageBox.Show("Please fill in Email and Role.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var user = new UserModel
            {
                Email = EmailTextBox.Text.Trim(),
                Password = PasswordBox.Password.Trim(),
                Role = RoleComboBox.Text.Trim(),
                EmployeeId = EmployeeComboBox.SelectedValue is int id ? id : (int?)null
            };

            if (_isEditMode && _editingUser != null)
            {
                user.Id = _editingUser.Id;
                bool success = _userService.UpdateUser(user);
                if (success)
                {
                    MessageBox.Show("User updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    OnUserSaved?.Invoke();
                    Cancel_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Failed to update user.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                bool success = _userService.AddUser(user);
                if (success)
                {
                    MessageBox.Show("User added successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    OnUserSaved?.Invoke();
                    Cancel_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Failed to add user.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
