using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.ViewModels
{
    public class EmployeePayslipViewModel : BaseViewModel
    {
        // ===== Data =====
        private readonly PayslipService _service = new PayslipService();

        public ObservableCollection<PayslipModel> Payslips { get; private set; } = new();
        private ObservableCollection<PayslipModel> _allPayslips = new();

        public ObservableCollection<PayslipRequestModel> MyRequests { get; private set; } = new();

        // ===== State =====
        private int _employeeId = 1; // set from login/session
        public int EmployeeId
        {
            get => _employeeId;
            set { _employeeId = value; OnPropertyChanged(); }
        }

        private string _employeeFullName = "";
        public string EmployeeFullName
        {
            get => _employeeFullName;
            set { _employeeFullName = value; OnPropertyChanged(); }
        }

        private int _selectedPayrollId;
        public int SelectedPayrollId
        {
            get => _selectedPayrollId;
            set { _selectedPayrollId = value; OnPropertyChanged(); }
        }

        private string _requestReason = "";
        public string RequestReason
        {
            get => _requestReason;
            set { _requestReason = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                (SubmitRequestCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        // ===== Commands =====
        public ICommand SubmitRequestCommand { get; }
        public ICommand RefreshCommand { get; }

        public EmployeePayslipViewModel()
        {
            SubmitRequestCommand = new RelayCommand(async _ => await SubmitPayslipRequestAsync(), _ => !IsBusy);
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
        }

        // ============================
        // Loading / Filtering
        // ============================

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            if (EmployeeId <= 0)
            {
                StatusMessage = "Missing Employee ID.";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Refreshing…";

                // Fetch in background so UI stays responsive
                var payslipsTask = Task.Run(() => _service.GetEmployeePayslips(EmployeeId), ct);
                var requestsTask = Task.Run(() => _service.GetRequestsByEmployee(EmployeeId), ct);

                await Task.WhenAll(payslipsTask, requestsTask);

                // Update collections atomically on UI thread
                _allPayslips = new ObservableCollection<PayslipModel>(payslipsTask.Result.OrderByDescending(p => p.PayDate));
                ReplaceCollection(Payslips, _allPayslips);

                var myReqsOrdered = requestsTask.Result.OrderByDescending(r => r.RequestDate).ToList();
                ReplaceCollection(MyRequests, new ObservableCollection<PayslipRequestModel>(myReqsOrdered));

                StatusMessage = "Data refreshed.";
            }
            catch (OperationCanceledException) { /* ignore */ }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void LoadPayslipsFromDatabase()
        {
            // kept for backward compatibility (sync call wrapping the async path)
            // Uses EmployeeId (defaults to 1 if unset), same as before.
            try
            {
                var data = _service.GetEmployeePayslips(EmployeeId > 0 ? EmployeeId : 1);
                _allPayslips = new ObservableCollection<PayslipModel>(data.OrderByDescending(p => p.PayDate));
                ReplaceCollection(Payslips, _allPayslips);
                StatusMessage = $"Loaded {Payslips.Count} payslip(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
        }

        public void FilterPayslips(string filterText)
        {
            if (_allPayslips == null || _allPayslips.Count == 0)
            {
                ReplaceCollection(Payslips, new ObservableCollection<PayslipModel>());
                OnPropertyChanged(nameof(Payslips));
                return;
            }

            if (string.IsNullOrWhiteSpace(filterText))
            {
                ReplaceCollection(Payslips, _allPayslips);
            }
            else
            {
                var f = filterText.Trim();
                var filtered = _allPayslips.Where(p =>
                        p.PayDate.ToString("d").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        p.HoursWorked.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        p.NetSalary.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                ReplaceCollection(Payslips, new ObservableCollection<PayslipModel>(filtered));
            }

            OnPropertyChanged(nameof(Payslips));
        }

        // ============================
        // Submit Request
        // ============================

        private async Task SubmitPayslipRequestAsync()
        {
            if (EmployeeId <= 0)
            {
                StatusMessage = "Missing Employee ID.";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Submitting request…";

                // Prevent duplicate Pending for same employee (and same payroll if specified)
                var hasPendingDuplicate = MyRequests.Any(r =>
                    r.EmployeeId == EmployeeId &&
                    r.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                    (SelectedPayrollId <= 0 || r.PayrollId == SelectedPayrollId));

                if (hasPendingDuplicate)
                {
                    StatusMessage = "You already have a pending request.";
                    return;
                }

                var req = new PayslipRequestModel
                {
                    EmployeeId = EmployeeId,
                    FullName = string.IsNullOrWhiteSpace(EmployeeFullName) ? "Employee" : EmployeeFullName.Trim(),
                    PayrollId = SelectedPayrollId, // allowed to be 0 if unknown
                    Reason = string.IsNullOrWhiteSpace(RequestReason) ? null : RequestReason.Trim(),
                    Status = "Pending",
                    RequestDate = DateTime.Now
                };

                var ok = await Task.Run(() => _service.CreatePayslipRequest(req));
                if (!ok)
                {
                    StatusMessage = "Failed to submit request.";
                    return;
                }

                // Refresh just requests list (lighter than full refresh)
                var updated = await Task.Run(() => _service.GetRequestsByEmployee(EmployeeId));
                var ordered = updated.OrderByDescending(r => r.RequestDate).ToList();
                ReplaceCollection(MyRequests, new ObservableCollection<PayslipRequestModel>(ordered));

                // reset inputs
                RequestReason = "";
                SelectedPayrollId = 0;

                StatusMessage = "Payslip request submitted.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ============================
        // Utils
        // ============================

        private static void ReplaceCollection<T>(ObservableCollection<T> target, ObservableCollection<T> source)
        {
            if (ReferenceEquals(target, source))
                return;

            target.Clear();
            foreach (var item in source)
                target.Add(item);
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => base.OnPropertyChanged(name);
    }

    // Simple RelayCommand (same API you used)
    public sealed class RelayCommand : ICommand
    {
        private readonly Predicate<object?>? _canExecute;
        private readonly Func<object?, Task>? _executeAsync;
        private readonly Action<object?>? _execute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public RelayCommand(Func<object?, Task> executeAsync, Predicate<object?>? canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public async void Execute(object? parameter)
        {
            if (_execute != null) _execute(parameter);
            else if (_executeAsync != null) await _executeAsync(parameter);
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
