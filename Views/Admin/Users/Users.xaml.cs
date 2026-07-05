using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SihyuPOSPayroll.Views.Admin.Users
{
    public partial class Users : UserControl
    {
        private readonly UserService _userService = new();
        private List<UserModel> _allUsers = new();

        public Users()
        {
            InitializeComponent();
            LoadUsers();
        }

        private void LoadUsers()
        {
            _allUsers = _userService.GetAllUsers();
            UserDataGrid.ItemsSource = _allUsers;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string search = SearchBox.Text.Trim().ToLower();

            var filtered = _allUsers.Where(u =>
                (u.Email?.ToLower().Contains(search) ?? false) ||
                (u.Role?.ToLower().Contains(search) ?? false) ||
                (u.Employee?.FullName?.ToLower().Contains(search) ?? false)
            ).ToList();

            UserDataGrid.ItemsSource = filtered;
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var addUserPopup = new AddEditUser();

            addUserPopup.OnUserSaved += () =>
            {
                LoadUsers();
            };

            // add to RootGrid (which we’ll add to XAML below)
            RootGrid.Children.Add(addUserPopup);
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UserModel user)
            {
                var editUserPopup = new AddEditUser(user); // pass user to edit

                editUserPopup.OnUserSaved += () =>
                {
                    LoadUsers(); // refresh list after update
                };

                RootGrid.Children.Add(editUserPopup);
            }
        }


        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UserModel user)
            {
                var confirm = MessageBox.Show($"Delete {user.Email}?", "Confirm", MessageBoxButton.YesNo);
                if (confirm == MessageBoxResult.Yes)
                {
                    if (_userService.DeleteUserById(user.Id))
                    {
                        LoadUsers();
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete.");
                    }
                }
            }
        }
    }
}
