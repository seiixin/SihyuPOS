// Views/Admin/Payroll/AddEditPayroll.xaml.cs
#nullable enable
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SihyuPOSPayroll.Views.Admin.Payrolls
{
    public partial class AddEditPayroll : UserControl
    {
        private readonly PayrollService _payrollService = new();
        private readonly EmployeeService _employeeService = new();
        private readonly AttendanceService _attendanceService = new(); // <-- use for worked days

        private readonly bool _isEditMode;
        private readonly PayrollModel? _editingPayroll;

        public List<EmployeeModel> Employees { get; set; } = new();

        public delegate void PayrollSavedHandler();
        public event PayrollSavedHandler? OnPayrollSaved;

        // Let the host close the overlay cleanly
        public event Action? CloseRequested;

        public AddEditPayroll(PayrollModel? payroll = null)
        {
            InitializeComponent();

            DataContext = this;

            // Load employees
            Employees = _employeeService.GetAllEmployees();
            EmployeeComboBox.ItemsSource = Employees;

            // Hook change events that should trigger recalculation
            EmployeeComboBox.SelectionChanged += RecalcInputs_Changed;
            StartDatePicker.SelectedDateChanged += RecalcInputs_Changed;
            EndDatePicker.SelectedDateChanged += RecalcInputs_Changed;

            if (payroll != null)
            {
                _isEditMode = true;
                _editingPayroll = payroll;

                TitleText.Text = "Edit Payroll";

                EmployeeComboBox.SelectedValue = payroll.EmployeeId;
                StartDatePicker.SelectedDate = payroll.StartDate;
                EndDatePicker.SelectedDate = payroll.EndDate;

                // We'll overwrite DaysWorkedTextBox via RecalculateDaysWorked(), but set initial visible values:
                DaysWorkedTextBox.Text = payroll.TotalDaysWorked.ToString();

                GrossSalaryTextBox.Text = payroll.GrossSalary.ToString("0.00");
                SSSTextBox.Text = (payroll?.SssDeduction ?? 0).ToString("0.00");
                PhilhealthTextBox.Text = payroll.PhilhealthDeduction.ToString("0.00");
                PagibigTextBox.Text = payroll.PagibigDeduction.ToString("0.00");
                OtherDeductionsTextBox.Text = payroll.OtherDeductions.ToString("0.00");
                BonusTextBox.Text = payroll.Bonus.ToString("0.00");
                NetSalaryTextBox.Text = payroll.NetSalary.ToString("0.00");
                BranchNameTextBox.Text = payroll.BranchName;
                ShiftTypeTextBox.Text = payroll.ShiftType;

                // Ensure we reflect the latest attendance-based value
                RecalculateDaysWorked();
            }
            else
            {
                _isEditMode = false;
                TitleText.Text = "Add New Payroll";

                // If dates already have defaults (e.g., today), compute immediately
                RecalculateDaysWorked();
            }
        }

        // ========================
        // Event wiring
        // ========================

        private void RecalcInputs_Changed(object sender, RoutedEventArgs e)
        {
            RecalculateDaysWorked();
        }

        // ========================
        // Core: attendance ? days
        // ========================

        /// <summary>
        /// Reads current Employee/Start/End inputs, then calls AttendanceService.GetWorkedDaysCount(...)
        /// which counts dates with BOTH time_in AND time_out within the range.
        /// </summary>
        private void RecalculateDaysWorked()
        {
            if (!TryGetInputs(out var employeeId, out var start, out var end))
            {
                DaysWorkedTextBox.Text = "0";
                return;
            }

            // Guard: date order
            if (end < start)
            {
                DaysWorkedTextBox.Text = "0";
                return;
            }

            try
            {
                // ? Single source of truth:
                // DISTINCT dates WHERE time_in IS NOT NULL AND time_out IS NOT NULL
                int days = _attendanceService.GetWorkedDaysCount(employeeId, start, end);
                DaysWorkedTextBox.Text = days.ToString();
            }
            catch (Exception ex)
            {
                // Non-fatal; reflect unknown as 0 and log
                DaysWorkedTextBox.Text = "0";
                Console.Error.WriteLine("Failed to recalc days worked: " + ex.Message);
            }
        }

        private bool TryGetInputs(out int employeeId, out DateTime start, out DateTime end)
        {
            employeeId = 0;
            start = default;
            end = default;

            if (EmployeeComboBox.SelectedValue is int eid)
                employeeId = eid;
            else
                return false;

            if (StartDatePicker.SelectedDate is DateTime s)
                start = s.Date;
            else
                return false;

            if (EndDatePicker.SelectedDate is DateTime e)
                end = e.Date;
            else
                return false;

            return true;
        }

        // ========================
        // Buttons
        // ========================

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Re-validate inputs
            if (!TryGetInputs(out var employeeId, out var startDate, out var endDate))
            {
                MessageBox.Show("Please select Employee, Start Date, and End Date.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (endDate < startDate)
            {
                MessageBox.Show("End Date cannot be earlier than Start Date.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // IMPORTANT: always compute from AttendanceService (ignore any stale UI value)
            int totalDaysWorked;
            try
            {
                totalDaysWorked = _attendanceService.GetWorkedDaysCount(employeeId, startDate, endDate);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to compute Total Days Worked on save: " + ex.Message);
                MessageBox.Show("Unable to compute Total Days Worked from attendance.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Push the computed value to UI (for user feedback) before saving
            DaysWorkedTextBox.Text = totalDaysWorked.ToString();

            var payroll = new PayrollModel
            {
                EmployeeId = employeeId,
                StartDate = startDate,
                EndDate = endDate,
                TotalDaysWorked = totalDaysWorked, // authoritative value
                GrossSalary = decimal.TryParse(GrossSalaryTextBox.Text, out var g) ? g : 0,
                SssDeduction = decimal.TryParse(SSSTextBox.Text, out var s) ? s : 0,
                PhilhealthDeduction = decimal.TryParse(PhilhealthTextBox.Text, out var ph) ? ph : 0,
                PagibigDeduction = decimal.TryParse(PagibigTextBox.Text, out var pi) ? pi : 0,
                OtherDeductions = decimal.TryParse(OtherDeductionsTextBox.Text, out var od) ? od : 0,
                Bonus = decimal.TryParse(BonusTextBox.Text, out var b) ? b : 0,
                NetSalary = decimal.TryParse(NetSalaryTextBox.Text, out var n) ? n : 0,
                BranchName = BranchNameTextBox.Text,
                ShiftType = ShiftTypeTextBox.Text
            };

            bool success;
            if (_isEditMode && _editingPayroll != null)
            {
                payroll.Id = _editingPayroll.Id;
                success = _payrollService.UpdatePayroll(payroll);
                if (!success)
                {
                    MessageBox.Show("Failed to update payroll.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                MessageBox.Show("Payroll updated successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                success = _payrollService.AddPayroll(payroll);
                if (!success)
                {
                    MessageBox.Show("Failed to add payroll.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                MessageBox.Show("Payroll added successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            OnPayrollSaved?.Invoke();
            CloseRequested?.Invoke();
        }
    }
}
