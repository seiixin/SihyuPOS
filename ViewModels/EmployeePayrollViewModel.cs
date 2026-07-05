#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.ViewModels
{
    public sealed class EmployeePayrollRecordsViewModel : INotifyPropertyChanged
    {
        private readonly PayrollService _payrollService = new();
        private readonly EmployeeService _employeeService = new();

        private int? _employeeId;
        public int? EmployeeId
        {
            get => _employeeId;
            private set { _employeeId = value; OnPropertyChanged(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value ?? string.Empty; OnPropertyChanged(); Refresh(); }
        }

        private string _lastMessage = string.Empty;
        public string LastMessage
        {
            get => _lastMessage;
            private set { _lastMessage = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PayrollModel> Payrolls { get; } = new();

        public ICommand RefreshCommand { get; }

        public EmployeePayrollRecordsViewModel()
        {
            RefreshCommand = new RelayCommand(_ => Refresh());
        }

        public void SetEmployee(int employeeId)
        {
            EmployeeId = employeeId > 0 ? employeeId : (int?)null;
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                var list = _payrollService.GetAllPayrolls();

                var query = list.AsEnumerable();

                if (EmployeeId.HasValue)
                    query = query.Where(p => p.EmployeeId == EmployeeId.Value);

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var s = SearchText.Trim().ToLowerInvariant();
                    query = query.Where(p =>
                        (p.EmployeeFullName ?? string.Empty).ToLower().Contains(s) ||
                        (p.BranchName ?? string.Empty).ToLower().Contains(s) ||
                        (p.ShiftType ?? string.Empty).ToLower().Contains(s) ||
                        p.StartDate.ToString("yyyy-MM-dd").Contains(s) ||
                        p.EndDate.ToString("yyyy-MM-dd").Contains(s));
                }

                var ordered = query
                    .OrderByDescending(p => p.EndDate)
                    .ThenByDescending(p => p.Id)
                    .ToList();

                Payrolls.Clear();
                foreach (var p in ordered) Payrolls.Add(p);

                LastMessage = $"Loaded {Payrolls.Count} payroll record(s).";
            }
            catch (Exception ex)
            {
                LastMessage = "Failed to load payroll records.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
