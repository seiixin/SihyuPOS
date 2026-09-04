#nullable enable
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SihyuPOSPayroll.Views.Admin.Users
{
    public partial class AddEditUser : UserControl
    {
        private readonly UserService    _userService    = new();
        private readonly EmployeeService _employeeService = new();

        private readonly bool       _isEditMode;
        private readonly UserModel? _editingUser;

        /// <summary>Raised when the dialog should close. bool = saved successfully.</summary>
        public event EventHandler<bool>? DialogClosed;

        /// <summary>Kept for backwards-compat; fires after a successful save.</summary>
        public delegate void UserSavedHandler();
        public event UserSavedHandler? OnUserSaved;

        public AddEditUser(UserModel? user = null)
        {
            InitializeComponent();
            PopulateRoles();
            PopulateEmployees();

            // Esc → cancel
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Close(saved: false);
                }
            };

            if (user != null)
            {
                _isEditMode  = true;
                _editingUser = user;

                TitleText.Text    = "Edit User";
                SubtitleText.Text = "Update the account details below.";
                PasswordHint.Text = "Leave blank to keep the existing password.";

                EmailTextBox.Text    = user.Email ?? string.Empty;
                PasswordBox.Password = string.Empty;
                RoleComboBox.Text    = user.Role  ?? string.Empty;

                // Set selected employee after Items are populated — find by Tag
                if (user.EmployeeId.HasValue)
                {
                    foreach (ComboBoxItem item in EmployeeComboBox.Items)
                    {
                        if (item.Tag is int id && id == user.EmployeeId.Value)
                        {
                            EmployeeComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }            }
            else
            {
                _isEditMode       = false;
                TitleText.Text    = "Add New User";
                SubtitleText.Text = "Fill in the details below to create a new account.";
                PasswordHint.Text = string.Empty;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void PopulateRoles()
        {
            try
            {
                var roles = new RoleService().GetAllRoles()
                                             .Select(r => r.Name)
                                             .ToList();
                RoleComboBox.ItemsSource = roles;
            }
            catch
            {
                // Fallback to built-in roles if DB is unavailable
                RoleComboBox.ItemsSource = new List<string> { "Admin", "Cashier", "Employee" };
            }
        }

        private void PopulateEmployees()
        {
            try
            {
                var employees = _employeeService.GetAllEmployees();

                // Populate with ComboBoxItem — WPF always displays Content correctly,
                // no DisplayMemberPath reflection needed. Tag holds the int? employee Id.
                EmployeeComboBox.Items.Clear();

                var noneItem = new ComboBoxItem
                {
                    Content = "— None —",
                    Tag     = (int?)null,
                };
                EmployeeComboBox.Items.Add(noneItem);
                EmployeeComboBox.SelectedItem = noneItem;

                foreach (var emp in employees)
                {
                    EmployeeComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = string.IsNullOrWhiteSpace(emp.FullName)
                                  ? $"Employee #{emp.Id}"
                                  : emp.FullName,
                        Tag = (int?)emp.Id,
                    });
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to load employees: " + ex.Message);
            }
        }

        private void Close(bool saved)
        {
            DialogClosed?.Invoke(this, saved);

            // Legacy: also remove from parent panel if used the old way
            if (Parent is Panel parent)
                parent.Children.Remove(this);
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close(saved: false);

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                ToastService.Warning("Email is required.");
                EmailTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(RoleComboBox.Text))
            {
                ToastService.Warning("Please select a Role.");
                RoleComboBox.Focus();
                return;
            }

            if (!_isEditMode && string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ToastService.Warning("Password is required for new users.");
                PasswordBox.Focus();
                return;
            }

            var user = new UserModel
            {
                Email      = EmailTextBox.Text.Trim(),
                Password   = PasswordBox.Password.Trim(),
                Role       = RoleComboBox.Text.Trim(),
                EmployeeId = (EmployeeComboBox.SelectedItem as ComboBoxItem)?.Tag is int eid
                             ? eid : (int?)null,
            };

            if (_isEditMode && _editingUser != null)
            {
                user.Id = _editingUser.Id;
                bool ok = _userService.UpdateUser(user);
                if (ok)
                {
                    OnUserSaved?.Invoke();
                    Close(saved: true);
                    ToastService.Success($"User '{user.Email}' updated.");
                }
                else
                {
                    ToastService.Error("Failed to update user.");
                }
            }
            else
            {
                bool ok = _userService.AddUser(user);
                if (ok)
                {
                    OnUserSaved?.Invoke();
                    Close(saved: true);
                    ToastService.Success($"User '{user.Email}' added.");
                }
                else
                {
                    ToastService.Error("Failed to add user.");
                }
            }
        }
    }
}
