using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SihyuPOSPayroll.Views.Admin.Users
{
    public partial class Users : UserControl
    {
        private readonly UserService _userService = new();
        private readonly EmployeeService _employeeService = new();
        private List<UserModel> _allUsers = new();

        public Users()
        {
            InitializeComponent();
            LoadUsers();
        }

        private void LoadUsers()
        {
            try
            {
                _allUsers = _userService.GetAllUsers();
                UserDataGrid.ItemsSource = _allUsers;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load users.\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim().ToLower();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allUsers
                : _allUsers.Where(user =>
                    (!string.IsNullOrEmpty(user.Email) && user.Email.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(user.Role) && user.Role.ToLower().Contains(query)) ||
                    (user.Employee != null && !string.IsNullOrEmpty(user.Employee.FullName) && user.Employee.FullName.ToLower().Contains(query))
                ).ToList();

            UserDataGrid.ItemsSource = filtered;
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var addUserPopup = new AddEditUser();
            addUserPopup.OnUserSaved += () => LoadUsers();
            RootGrid.Children.Add(addUserPopup);
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UserModel user)
            {
                var editUserPopup = new AddEditUser(user);
                editUserPopup.OnUserSaved += () => LoadUsers();
                RootGrid.Children.Add(editUserPopup);
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UserModel user)
            {
                var confirm = MessageBox.Show($"Are you sure you want to delete {user.Email}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                {
                    try
                    {
                        bool success = _userService.DeleteUserById(user.Id);
                        if (success)
                        {
                            MessageBox.Show($"Deleted user: {user.Email}");
                            LoadUsers();
                        }
                        else
                        {
                            MessageBox.Show("Failed to delete user.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error deleting user:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
