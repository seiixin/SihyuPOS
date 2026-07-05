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
            LoadEmployees();
        }

        private void LoadEmployees()
        {
            try
            {
                _allEmployees = _employeeService.GetAllEmployees();
                EmployeeDataGrid.ItemsSource = _allEmployees;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load employees.\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim().ToLower();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allEmployees
                : _allEmployees.Where(emp =>
                    (!string.IsNullOrEmpty(emp.FullName) && emp.FullName.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(emp.Position) && emp.Position.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(emp.ContactNumber) && emp.ContactNumber.ToLower().Contains(query)) ||
                    (emp.UserAccount != null && emp.UserAccount.Id.ToString().Contains(query))
                ).ToList();

            EmployeeDataGrid.ItemsSource = filtered;
        }

        private void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            var addEmployeePopup = new AddEditEmployee();

            addEmployeePopup.OnEmployeeSaved += () =>
            {
                LoadEmployees();
            };

            RootGrid.Children.Add(addEmployeePopup);
        }

        private void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is EmployeeModel employee)
            {
                var editEmployeePopup = new AddEditEmployee(employee);

                editEmployeePopup.OnEmployeeSaved += () =>
                {
                    LoadEmployees();
                };

                RootGrid.Children.Add(editEmployeePopup);
            }
        }

        private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is EmployeeModel employee)
            {
                var confirm = MessageBox.Show($"Are you sure you want to delete {employee.FullName}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (confirm == MessageBoxResult.Yes)
                {
                    bool success = false;
                    try
                    {
                        success = _employeeService.DeleteEmployee(employee.Id);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error deleting employee:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    if (success)
                    {
                        MessageBox.Show($"Deleted employee: {employee.FullName}");
                        LoadEmployees();
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete employee.");
                    }
                }
            }
        }
    }
}
