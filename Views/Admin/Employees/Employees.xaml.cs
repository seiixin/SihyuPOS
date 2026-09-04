using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SihyuPOSPayroll.Views.Admin.Employees
{
    public partial class Employees : UserControl
    {
        private readonly EmployeeService _employeeService = new();
        private List<EmployeeModel> _allEmployees = new();

        public Employees()
        {
            InitializeComponent();
            ToastService.Register(EmployeesToast);
            LoadEmployees();
        }

        // ── Data ─────────────────────────────────────────────────────────────

        private void LoadEmployees()
        {
            try
            {
                _allEmployees = _employeeService.GetAllEmployees();
                ApplyFilter(SearchBox?.Text ?? string.Empty);
            }
            catch (Exception ex)
            {
                ToastService.Error("Failed to load employees: " + ex.Message);
            }
        }

        private void ApplyFilter(string query)
        {
            query = query.Trim().ToLower();

            var list = string.IsNullOrWhiteSpace(query)
                ? _allEmployees
                : _allEmployees.Where(e =>
                      (e.FullName      != null && e.FullName.ToLower().Contains(query))      ||
                      (e.Position      != null && e.Position.ToLower().Contains(query))      ||
                      (e.ContactNumber != null && e.ContactNumber.ToLower().Contains(query)) ||
                      (e.Shift         != null && e.Shift.ToLower().Contains(query))
                  ).ToList();

            EmployeeDataGrid.ItemsSource = list;
            EmptyState.Visibility = list.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ── Overlay helpers ───────────────────────────────────────────────────

        private void OpenDialog(AddEditEmployee dialog)
        {
            dialog.DialogClosed += OnDialogClosed;
            DialogHost.Content       = dialog;
            DialogOverlay.Visibility = Visibility.Visible;
        }

        private void OnDialogClosed(object? sender, bool saved)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
            DialogHost.Content       = null;
            if (saved) LoadEmployees();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter(SearchBox.Text);

        private void AddEmployee_Click(object sender, RoutedEventArgs e)
            => OpenDialog(new AddEditEmployee());

        private void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is EmployeeModel emp)
                OpenDialog(new AddEditEmployee(emp));
        }

        private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not EmployeeModel emp)
                return;

            var confirm = MessageBox.Show(
                $"Delete '{emp.FullName}'?\nThis cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                if (_employeeService.DeleteEmployee(emp.Id))
                {
                    LoadEmployees();
                    ToastService.Info($"'{emp.FullName}' removed.");
                }
                else
                {
                    ToastService.Error("Failed to delete employee.");
                }
            }
            catch (Exception ex)
            {
                ToastService.Error("Error: " + ex.Message);
            }
        }
    }
}
