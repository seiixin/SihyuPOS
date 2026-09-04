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
        private List<UserModel> _allUsers = new();

        public Users()
        {
            InitializeComponent();
            ToastService.Register(UsersToast);
            LoadUsers();
        }

        // ── Data ─────────────────────────────────────────────────────────────

        private void LoadUsers()
        {
            try
            {
                _allUsers = _userService.GetAllUsers();
                ApplyFilter(SearchBox?.Text ?? string.Empty);
            }
            catch (Exception ex)
            {
                ToastService.Error("Failed to load users: " + ex.Message);
            }
        }

        private void ApplyFilter(string query)
        {
            query = query.Trim().ToLower();

            var list = string.IsNullOrWhiteSpace(query)
                ? _allUsers
                : _allUsers.Where(u =>
                      (u.Email    != null && u.Email.ToLower().Contains(query)) ||
                      (u.Role     != null && u.Role.ToLower().Contains(query))  ||
                      (u.Employee != null && u.Employee.FullName != null &&
                       u.Employee.FullName.ToLower().Contains(query))
                  ).ToList();

            UserDataGrid.ItemsSource = list;
            EmptyState.Visibility   = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Overlay helpers ───────────────────────────────────────────────────

        private void OpenDialog(AddEditUser dialog)
        {
            dialog.DialogClosed += OnDialogClosed;
            DialogHost.Content      = dialog;
            DialogOverlay.Visibility = Visibility.Visible;
        }

        private void OnDialogClosed(object? sender, bool saved)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
            DialogHost.Content       = null;
            if (saved) LoadUsers();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter(SearchBox.Text);

        private void AddUser_Click(object sender, RoutedEventArgs e)
            => OpenDialog(new AddEditUser());

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is UserModel user)
                OpenDialog(new AddEditUser(user));
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is UserModel user)
            {
                var confirm = MessageBox.Show(
                    $"Delete '{user.Email}'?\nThis cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes) return;

                try
                {
                    if (_userService.DeleteUserById(user.Id))
                    {
                        LoadUsers();
                        ToastService.Info($"User '{user.Email}' deleted.");
                    }
                    else
                    {
                        ToastService.Error("Failed to delete user.");
                    }
                }
                catch (Exception ex)
                {
                    ToastService.Error("Error: " + ex.Message);
                }
            }
        }
    }
}
