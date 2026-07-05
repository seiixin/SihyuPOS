using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;                 // XamlParseException
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.ViewModels;
using SihyuPOSPayroll.Views.Admin.Payrolls; // AddEditPayroll
using SihyuPOSPayroll.Views.Admin.Payroll;  // PositionSalaryEdit

namespace SihyuPOSPayroll.Views.Admin.Payroll
{
    public partial class Payroll : UserControl
    {
        private readonly PayrollService _payrollService = new();
        private readonly PayslipService _payslipService = new();
        private readonly PayrollViewModel _vm = new();

        public Payroll()
        {
            try
            {
                InitializeComponent();
            }
            catch (XamlParseException xpe)
            {
                var root = xpe.InnerException ?? xpe;
                MessageBox.Show(
                    $"UI load failed (XAML).\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Payroll", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"UI load failed.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Payroll", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DataContext = _vm;
            RefreshFromDatabase();
        }

        private void RefreshFromDatabase()
        {
            try
            {
                var data = _payrollService.GetAllPayrolls();
                _vm.LoadPayrolls(data);
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"Failed to load payrolls from DB.\n\n{root.GetType().Name}: {root.Message}",
                    "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filter = (sender as TextBox)?.Text ?? string.Empty;
            _vm.FilterPayroll(filter);
        }

        private void AddPayroll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var editor = new AddEditPayroll();
                editor.OnPayrollSaved += RefreshFromDatabase;
                editor.CloseRequested += CloseOverlay;
                ShowOverlay(editor);
            }
            catch (XamlParseException xpe)
            {
                var root = xpe.InnerException ?? xpe;
                MessageBox.Show(
                    $"Failed to open Add Payroll (XAML).\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"Failed to open Add Payroll.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditPayroll_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PayrollModel row) return;

            try
            {
                var editor = new AddEditPayroll(row);
                editor.OnPayrollSaved += RefreshFromDatabase;
                editor.CloseRequested += CloseOverlay;
                ShowOverlay(editor);
            }
            catch (XamlParseException xpe)
            {
                var root = xpe.InnerException ?? xpe;
                MessageBox.Show(
                    $"Failed to open Edit Payroll (XAML).\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"Failed to open Edit Payroll.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Open Positions & Salaries (safe DB checks; no unhandled throws) =====
        private void OpenPositionSalary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // If you have a centralized conn string (e.g., App.DatabaseServices), pass it here.
                var service = new PositionSalaryService();

                // Ensure schema exists (non-throwing)
                if (!service.TryEnsureSchema())
                {
                    MessageBox.Show(
                        "Positions & Salaries isn't ready (schema).\n\n" + (service.LastError ?? "Unknown error."),
                        "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Probe accessibility (non-throwing)
                if (!service.IsDbReady())
                {
                    MessageBox.Show(
                        "Positions & Salaries table is not accessible.\n\n" + (service.LastError ?? "Unknown error."),
                        "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Open the editor; the view will call VM.Load() on Loaded
                var vm = new PositionSalaryViewModel(service);

                PositionSalaryEdit setup;
                try
                {
                    setup = new PositionSalaryEdit(vm);
                }
                catch (XamlParseException xpe)
                {
                    var root = xpe.InnerException ?? xpe;
                    MessageBox.Show(
                        $"Failed to open Positions & Salaries (XAML).\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                catch (Exception ex2)
                {
                    var root = ex2.InnerException ?? ex2;
                    MessageBox.Show(
                        $"Failed to open Positions & Salaries.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                setup.CloseRequested += CloseOverlay;
                ShowOverlay(setup);
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show("Failed to open Positions & Salaries.\n\n" +
                                $"{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool DeletePayrollById(int id)
        {
            try
            {
                var ok = _payrollService.DeletePayrollById(id);
                if (ok) RefreshFromDatabase();
                return ok;
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"Delete failed.\n\n{root.GetType().Name}: {root.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ===== Overlay host utilities =====
        private void ShowOverlay(UserControl editor)
        {
            try
            {
                var host = RootGrid.Children
                                   .OfType<ContentControl>()
                                   .FirstOrDefault(c => c.Name == "OverlayHost");
                if (host == null)
                {
                    host = new ContentControl { Name = "OverlayHost" };
                    Grid.SetRow(host, 3); // overlay above the DataGrid row
                    RootGrid.Children.Add(host);
                }
                host.Content = editor;
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"Failed to show overlay.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseOverlay()
        {
            try
            {
                var host = RootGrid.Children
                                   .OfType<ContentControl>()
                                   .FirstOrDefault(c => c.Name == "OverlayHost");
                if (host != null) host.Content = null;
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"Failed to close overlay.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Generate Payroll =====
        private void GeneratePayroll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var now = DateTime.Now;
                var choice = ((PeriodCombo.SelectedItem as ComboBoxItem)?.Content as string) ?? "FirstHalf";

                DateTime start, end;

                if (string.Equals(choice, "Custom", StringComparison.OrdinalIgnoreCase))
                {
                    if (CustomStartPicker.SelectedDate is not DateTime s ||
                        CustomEndPicker.SelectedDate is not DateTime t)
                    {
                        MessageBox.Show("Please select Custom Start and End dates.", "Missing Dates",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (t.Date < s.Date)
                    {
                        MessageBox.Show("End date must be on/after Start date.", "Invalid Range",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    start = s.Date; end = t.Date;
                }
                else if (string.Equals(choice, "SecondHalf", StringComparison.OrdinalIgnoreCase))
                {
                    (start, end) = _payrollService.GetSecondHalfRange(now.Year, now.Month);
                }
                else
                {
                    (start, end) = _payrollService.GetFirstHalfRange(now.Year, now.Month);
                }

                var generated = _payrollService.GenerateForPeriod(start, end);
                if (generated.Count == 0)
                {
                    MessageBox.Show($"No payroll rows generated for {start:yyyy-MM-dd} to {end:yyyy-MM-dd}.",
                        "Nothing to Save", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveOk = _payrollService.SaveGeneratedPayrolls(generated);
                if (!saveOk)
                {
                    MessageBox.Show("Failed to save generated payroll rows.", "Save Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var createdSlips = _payslipService.CreatePayslipsFromPayrollPeriod(start, end);

                MessageBox.Show(
                    $"Generated payroll for {start:yyyy-MM-dd} ? {end:yyyy-MM-dd}\n" +
                    $"Rows: {generated.Count}\n" +
                    $"Payslips created: {createdSlips}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                RefreshFromDatabase();
            }
            catch (Exception ex)
            {
                var root = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"Error generating payroll.\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
    }
}
