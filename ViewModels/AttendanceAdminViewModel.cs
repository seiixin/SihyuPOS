using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace SihyuPOSPayroll.ViewModels
{
    public class AttendanceAdminViewModel : INotifyPropertyChanged
    {
        // ---------------- Attendance ----------------
        public ObservableCollection<AttendanceModel> Attendances { get; } = new();

        private AttendanceModel? _selectedAttendance;
        public AttendanceModel? SelectedAttendance
        {
            get => _selectedAttendance;
            set { _selectedAttendance = value; OnChanged(nameof(SelectedAttendance)); Requery(); }
        }

        private DateTime? _filterDate;
        public DateTime? FilterDate
        {
            get => _filterDate;
            set { _filterDate = value; OnChanged(nameof(FilterDate)); }
        }

        private string? _filterEmployeeId;
        public string? FilterEmployeeId
        {
            get => _filterEmployeeId;
            set { _filterEmployeeId = value; OnChanged(nameof(FilterEmployeeId)); }
        }

        public ICommand FilterCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }

        // ---------------- Leave ----------------
        public ObservableCollection<LeaveRequestModel> LeaveRequests { get; } = new();

        private LeaveRequestModel? _selectedLeave;
        public LeaveRequestModel? SelectedLeave
        {
            get => _selectedLeave;
            set { _selectedLeave = value; OnChanged(nameof(SelectedLeave)); Requery(); }
        }

        private string? _leaveFilterEmployeeId;
        public string? LeaveFilterEmployeeId
        {
            get => _leaveFilterEmployeeId;
            set { _leaveFilterEmployeeId = value; OnChanged(nameof(LeaveFilterEmployeeId)); }
        }

        private DateTime? _leaveFrom;
        public DateTime? LeaveFrom
        {
            get => _leaveFrom;
            set { _leaveFrom = value; OnChanged(nameof(LeaveFrom)); }
        }

        private DateTime? _leaveTo;
        public DateTime? LeaveTo
        {
            get => _leaveTo;
            set { _leaveTo = value; OnChanged(nameof(LeaveTo)); }
        }

        private LeaveStatus? _leaveFilterStatus;
        public LeaveStatus? LeaveFilterStatus
        {
            get => _leaveFilterStatus;
            set { _leaveFilterStatus = value; OnChanged(nameof(LeaveFilterStatus)); }
        }

        public Array LeaveStatuses { get; } = Enum.GetValues(typeof(LeaveStatus));

        public ICommand LeaveFilterCommand { get; }
        public ICommand LeaveCreateCommand { get; }
        public ICommand LeaveUpdateCommand { get; }
        public ICommand LeaveDeleteCommand { get; }
        public ICommand LeaveApproveCommand { get; }
        public ICommand LeaveRejectCommand { get; }
        public ICommand LeaveCancelCommand { get; }

        private readonly AttendanceService _attendanceService = new();
        private readonly ILeaveRequestService _leaveService = new LeaveRequestService();

        public AttendanceAdminViewModel()
        {
            // Attendance
            FilterCommand = new RelayCommand(_ => FilterAttendance());
            AddCommand = new RelayCommand(_ => AddAttendance(), _ => SelectedAttendance != null);
            UpdateCommand = new RelayCommand(_ => UpdateAttendance(), _ => SelectedAttendance != null);
            DeleteCommand = new RelayCommand(_ => DeleteAttendance(), _ => SelectedAttendance != null);

            // Leave
            LeaveFilterCommand = new RelayCommand(_ => FilterLeave());
            LeaveCreateCommand = new RelayCommand(_ => CreateLeave(),
                                   _ => SelectedLeave is { EmployeeId: > 0, DateFrom: var df, DateTo: var dt } && df <= dt);
            LeaveUpdateCommand = new RelayCommand(_ => UpdateLeave(),
                                   _ => SelectedLeave is { Id: > 0 }); // allow updating status or fields
            LeaveDeleteCommand = new RelayCommand(_ => DeleteLeave(),
                                   _ => SelectedLeave is { Id: > 0 });
            LeaveApproveCommand = new RelayCommand(_ => ApproveLeave(),
                                   _ => SelectedLeave is { Id: > 0, Status: LeaveStatus.Pending });
            LeaveRejectCommand = new RelayCommand(_ => RejectLeave(),
                                   _ => SelectedLeave is { Id: > 0, Status: LeaveStatus.Pending });
            LeaveCancelCommand = new RelayCommand(_ => CancelLeave(),
                                   _ => SelectedLeave is { Id: > 0 } && (SelectedLeave!.Status == LeaveStatus.Pending || SelectedLeave!.Status == LeaveStatus.Approved));

            // initial loads
            FilterAttendance();
            FilterLeave();
        }

        // -------- Attendance ops --------
        private void FilterAttendance()
        {
            int? empId = null;
            if (int.TryParse(FilterEmployeeId, out var parsedId)) empId = parsedId;

            var results = _attendanceService.GetAttendances(FilterDate, empId);
            Attendances.Clear();
            foreach (var a in results) Attendances.Add(a);
        }

        private void AddAttendance()
        {
            if (SelectedAttendance == null) return;
            _attendanceService.AddAttendance(SelectedAttendance);
            FilterAttendance();
        }

        private void UpdateAttendance()
        {
            if (SelectedAttendance == null) return;
            _attendanceService.UpdateAttendance(SelectedAttendance);
            FilterAttendance();
        }

        private void DeleteAttendance()
        {
            if (SelectedAttendance == null) return;
            _attendanceService.DeleteAttendance(SelectedAttendance.Id);
            FilterAttendance();
        }

        // -------- Leave ops --------
        private void FilterLeave()
        {
            LeaveRequests.Clear();

            var hasEmp = int.TryParse(LeaveFilterEmployeeId, out var empId) && empId > 0;

            var rows = hasEmp
                ? _leaveService.GetForEmployee(empId, LeaveFrom, LeaveTo, LeaveFilterStatus)
                : _leaveService.GetAll(LeaveFrom, LeaveTo, LeaveFilterStatus);

            foreach (var lr in rows) LeaveRequests.Add(lr);
        }

        private void CreateLeave()
        {
            if (SelectedLeave == null) return;

            try
            {
                var id = _leaveService.Create(SelectedLeave);
                if (id > 0)
                {
                    SelectedLeave.Id = id;
                    // refresh just this row
                    var fresh = _leaveService.GetById(id);
                    if (fresh != null)
                    {
                        LeaveRequests.Add(fresh);
                        SelectedLeave = fresh;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Create failed: " + ex.Message, "Leave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateLeave()
        {
            if (SelectedLeave == null) return;

            try
            {
                // Persist whatever is currently in the row (status/fields)
                var ok = _leaveService.Update(SelectedLeave);
                if (!ok)
                {
                    MessageBox.Show("No rows were updated. The record may have been deleted.", "Leave", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Re-fetch the updated row and swap it in-place (so grid shows DB-truth)
                var id = SelectedLeave.Id;
                var fresh = _leaveService.GetById(id);
                if (fresh != null)
                {
                    var idx = LeaveRequests.IndexOf(SelectedLeave);
                    if (idx >= 0)
                        LeaveRequests[idx] = fresh;
                    SelectedLeave = fresh;
                }

                // Do NOT auto-filter here, to avoid the row “disappearing” due to active filters.
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed: " + ex.Message, "Leave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteLeave()
        {
            if (SelectedLeave == null) return;

            try
            {
                if (_leaveService.Delete(SelectedLeave.Id))
                {
                    LeaveRequests.Remove(SelectedLeave);
                    SelectedLeave = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Delete failed: " + ex.Message, "Leave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApproveLeave()
        {
            if (SelectedLeave == null) return;

            try
            {
                var approverUserId = GetCurrentUserIdForApproval();
                if (_leaveService.Approve(SelectedLeave.Id, approverUserId))
                {
                    SelectedLeave.Status = LeaveStatus.Approved;
                    UpdateLeave(); // will refresh row from DB
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Approve failed: " + ex.Message, "Leave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RejectLeave()
        {
            if (SelectedLeave == null) return;

            try
            {
                var approverUserId = GetCurrentUserIdForApproval();
                if (_leaveService.Reject(SelectedLeave.Id, approverUserId))
                {
                    SelectedLeave.Status = LeaveStatus.Rejected;
                    UpdateLeave();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Reject failed: " + ex.Message, "Leave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelLeave()
        {
            if (SelectedLeave == null) return;

            try
            {
                if (_leaveService.Cancel(SelectedLeave.Id))
                {
                    SelectedLeave.Status = LeaveStatus.Cancelled;
                    UpdateLeave();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cancel failed: " + ex.Message, "Leave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetCurrentUserIdForApproval()
        {
            // TODO: wire to actual auth/session
            return 1;
        }

        // -------------- INotifyPropertyChanged --------------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private static void Requery() => CommandManager.InvalidateRequerySuggested();
    }
}
