using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Helpers; // RelayCommand<T>

namespace SihyuPOSPayroll.ViewModels
{
    public class PayrollViewModel : INotifyPropertyChanged
    {
        // Backing store of all items (unfiltered)
        private readonly List<PayrollModel> _allPayrolls = new();

        // Exposed to the DataGrid
        public ObservableCollection<PayrollModel> PayrollList { get; } = new();

        // Optional hook the host can supply to delete from DB: (payrollId) => bool success
        private readonly Func<int, bool>? _deleteById;

        public ICommand DeleteCommand { get; }

        /// <summary>
        /// Default: UI-only delete (no DB call). The host can still replace the collection by calling LoadPayrolls again.
        /// </summary>
        public PayrollViewModel() : this(deleteById: null) { }

        /// <summary>
        /// Preferred: supply a delegate to perform DB deletion. If it returns true, the item is removed from the UI.
        /// Usage from code-behind: new PayrollViewModel(id => _payrollService.DeletePayrollById(id));
        /// </summary>
        public PayrollViewModel(Func<int, bool>? deleteById)
        {
            _deleteById = deleteById;
            DeleteCommand = new RelayCommand<PayrollModel>(DeletePayroll, CanDelete);
        }

        private bool CanDelete(PayrollModel? payroll) => payroll != null;

        /// <summary>
        /// Replace the entire data set (from DB).
        /// </summary>
        public void LoadPayrolls(List<PayrollModel> payrolls)
        {
            // Reset full list
            _allPayrolls.Clear();
            if (payrolls != null && payrolls.Count > 0)
                _allPayrolls.AddRange(payrolls);

            // Reset visible list
            PayrollList.Clear();
            foreach (var p in _allPayrolls)
                PayrollList.Add(p);

            OnPropertyChanged(nameof(PayrollList));
        }

        /// <summary>
        /// Filter by employee id, name, branch, shift, or period dates (yyyy-MM-dd).
        /// </summary>
        public void FilterPayroll(string? filter)
        {
            var hasFilter = !string.IsNullOrWhiteSpace(filter);
            var needle = (filter ?? string.Empty).Trim();

            IEnumerable<PayrollModel> source = _allPayrolls;

            if (hasFilter)
            {
                var n = needle.ToLowerInvariant();

                source = _allPayrolls.Where(p =>
                    p.EmployeeId.ToString().Contains(n, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(p.EmployeeFullName) &&
                        p.EmployeeFullName!.ToLowerInvariant().Contains(n)) ||
                    (!string.IsNullOrWhiteSpace(p.BranchName) &&
                        p.BranchName!.ToLowerInvariant().Contains(n)) ||
                    (!string.IsNullOrWhiteSpace(p.ShiftType) &&
                        p.ShiftType!.ToLowerInvariant().Contains(n)) ||
                    p.StartDate.ToString("yyyy-MM-dd").Contains(n, StringComparison.OrdinalIgnoreCase) ||
                    p.EndDate.ToString("yyyy-MM-dd").Contains(n, StringComparison.OrdinalIgnoreCase)
                );
            }

            PayrollList.Clear();
            foreach (var p in source)
                PayrollList.Add(p);

            OnPropertyChanged(nameof(PayrollList));
        }

        private void DeletePayroll(PayrollModel? payroll)
        {
            if (payroll == null) return;

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete payroll for employee #{payroll.EmployeeId}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            // If a DB deleter was provided, call it
            if (_deleteById != null)
            {
                var ok = false;
                try
                {
                    ok = _deleteById.Invoke(payroll.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete payroll record.\n\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ok)
                {
                    MessageBox.Show("Failed to delete payroll record.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Remove from in-memory lists
            _allPayrolls.Remove(payroll);
            PayrollList.Remove(payroll);

            OnPropertyChanged(nameof(PayrollList));
        }

        // ===== INotifyPropertyChanged =====
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
