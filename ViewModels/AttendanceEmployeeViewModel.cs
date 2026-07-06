using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.ViewModels
{
    /// <summary>
    /// Employee Attendance VM:
    /// - Loads EmployeeName + UserId and exposes EmployeeDisplay: "Name (User #123)"
    /// - Builds a per-day calendar between FilterFromDate..FilterToDate
    ///   * "Present"  => only if BOTH TimeIn and TimeOut exist for that date
    ///   * "No Record" => no logs but date is a scheduled workday
    ///   * "Day Off"   => date is not in the assigned work schedule (days_mask)
    /// - Days worked (payroll) still rely on complete pairs only.
    /// </summary>
    public class AttendanceEmployeeViewModel : INotifyPropertyChanged
    {
        // ===== Services =====
        private readonly AttendanceService _attendanceService = new();
        private readonly ILeaveRequestService _leaveService = new LeaveRequestService();
        private readonly IEmployeeService _employeeService = new EmployeeService();

        // ===== Resolved identity (EMPLOYEE id, not user id) =====
        private int? _currentEmployeeId;
        public int? CurrentEmployeeId
        {
            get => _currentEmployeeId;
            private set { _currentEmployeeId = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        // ===== Employee header =====
        private string _employeeName = string.Empty;
        public string EmployeeName
        {
            get => _employeeName;
            private set { _employeeName = value; OnPropertyChanged(); OnPropertyChanged(nameof(EmployeeDisplay)); }
        }

        private int? _userId;
        public int? UserId
        {
            get => _userId;
            private set { _userId = value; OnPropertyChanged(); OnPropertyChanged(nameof(EmployeeDisplay)); }
        }

        public string EmployeeDisplay =>
            string.IsNullOrWhiteSpace(EmployeeName)
                ? (UserId.HasValue ? $"(User #{UserId.Value})" : string.Empty)
                : (UserId.HasValue ? $"{EmployeeName} (User #{UserId.Value})" : EmployeeName);

        // ===== Commands (Attendance) =====
        public ICommand ClockInCommand { get; private set; } = null!;
        public ICommand ClockOutCommand { get; private set; } = null!;
        public ICommand RefreshListCommand { get; private set; } = null!;
        public ICommand ApplyFilterCommand { get; private set; } = null!;
        public ICommand ResetFilterCommand { get; private set; } = null!;

        // ===== Commands (Leave) =====
        public ICommand LoadLeaveListCommand { get; private set; } = null!;
        public ICommand ApplyLeaveFilterCommand { get; private set; } = null!;
        public ICommand ResetLeaveFilterCommand { get; private set; } = null!;
        public ICommand NewLeaveFormCommand { get; private set; } = null!;
        public ICommand SubmitLeaveRequestCommand { get; private set; } = null!;
        public ICommand UpdateLeaveRequestCommand { get; private set; } = null!;
        public ICommand CancelLeaveRequestCommand { get; private set; } = null!;
        public ICommand EditSelectedLeaveCommand { get; private set; } = null!;

        // ===== UI State (Shared) =====
        private string _lastMessage = string.Empty;
        public string LastMessage
        {
            get => _lastMessage;
            set { _lastMessage = value; OnPropertyChanged(); }
        }

        private bool _isBusyAttendance;
        public bool IsBusyAttendance
        {
            get => _isBusyAttendance;
            set { _isBusyAttendance = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private bool _isBusyLeave;
        public bool IsBusyLeave
        {
            get => _isBusyLeave;
            set { _isBusyLeave = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        // =========================================================================================
        // ATTENDANCE SECTION
        // =========================================================================================

        private bool _isManualMode;
        public bool IsManualMode
        {
            get => _isManualMode;
            set { if (_isManualMode != value) { _isManualMode = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }

        private DateTime? _manualDate = DateTime.Today;
        public DateTime? ManualDate
        {
            get => _manualDate;
            set { if (_manualDate != value) { _manualDate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }

        // Accepts "HH:mm" or "HH:mm:ss"
        private string _manualTimeText = "09:00";
        public string ManualTimeText
        {
            get => _manualTimeText;
            set { if (_manualTimeText != value) { _manualTimeText = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }

        // PER-DAY rows for UI (includes DisplayStatus + EmployeeDisplay)
        public ObservableCollection<AttendanceDayRow> Attendances { get; } = new();

        private DateTime? _filterFromDate = DateTime.Today.AddDays(-30);
        public DateTime? FilterFromDate
        {
            get => _filterFromDate;
            set { if (_filterFromDate != value) { _filterFromDate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }

        private DateTime? _filterToDate = DateTime.Today;
        public DateTime? FilterToDate
        {
            get => _filterToDate;
            set { if (_filterToDate != value) { _filterToDate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }

        // =========================================================================================
        // LEAVE REQUESTS SECTION
        // =========================================================================================

        public ObservableCollection<LeaveRequestModel> LeaveRequests { get; } = new();

        private DateTime? _leaveFilterFrom = DateTime.Today.AddDays(-60);
        public DateTime? LeaveFilterFrom
        {
            get => _leaveFilterFrom;
            set { if (_leaveFilterFrom != value) { _leaveFilterFrom = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }

        private DateTime? _leaveFilterTo = DateTime.Today.AddDays(60);
        public DateTime? LeaveFilterTo
        {
            get => _leaveFilterTo;
            set { if (_leaveFilterTo != value) { _leaveFilterTo = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }

        private LeaveStatus? _leaveFilterStatus = null; // null = All
        public LeaveStatus? LeaveFilterStatus
        {
            get => _leaveFilterStatus;
            set { if (_leaveFilterStatus != value) { _leaveFilterStatus = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        }

        private LeaveRequestModel? _selectedLeave;
        public LeaveRequestModel? SelectedLeave
        {
            get => _selectedLeave;
            set { _selectedLeave = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private int _leaveFormId; // 0 => new
        public int LeaveFormId
        {
            get => _leaveFormId;
            set { _leaveFormId = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private LeaveType _leaveType = LeaveType.Sick;
        public LeaveType LeaveType
        {
            get => _leaveType;
            set { _leaveType = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private string? _leaveReason;
        public string? LeaveReason
        {
            get => _leaveReason;
            set { _leaveReason = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private DateTime _leaveDateFrom = DateTime.Today;
        public DateTime LeaveDateFrom
        {
            get => _leaveDateFrom;
            set { _leaveDateFrom = value.Date; OnPropertyChanged(); OnPropertyChanged(nameof(LeaveDurationDays)); CommandManager.InvalidateRequerySuggested(); }
        }

        private DateTime _leaveDateTo = DateTime.Today;
        public DateTime LeaveDateTo
        {
            get => _leaveDateTo;
            set { _leaveDateTo = value.Date; OnPropertyChanged(); OnPropertyChanged(nameof(LeaveDurationDays)); CommandManager.InvalidateRequerySuggested(); }
        }

        private bool _leaveHalfDay;
        public bool LeaveHalfDay
        {
            get => _leaveHalfDay;
            set { _leaveHalfDay = value; OnPropertyChanged(); OnPropertyChanged(nameof(LeaveDurationDays)); CommandManager.InvalidateRequerySuggested(); }
        }

        public string LeaveDurationDays
        {
            get
            {
                if (LeaveHalfDay) return "0.5 day";
                var days = (LeaveDateTo.Date - LeaveDateFrom.Date).TotalDays + 1;
                if (days < 0) return "—";
                return $"{days:0} day(s)";
            }
        }

        // =========================================================================================
        // ctors
        // =========================================================================================

        // Default: resolve employee via users.employee_id
        public AttendanceEmployeeViewModel()
        {
            ResolveEmployeeIdFromDb();
            SetupCommands();
            KickOffInitialLoads();
        }

        // Overload: allow passing employeeId directly
        public AttendanceEmployeeViewModel(int employeeId)
        {
            CurrentEmployeeId = employeeId > 0 ? employeeId : null;
            if (CurrentEmployeeId is null)
                ResolveEmployeeIdFromDb();

            SetupCommands();
            KickOffInitialLoads();
        }

        private void ResolveEmployeeIdFromDb()
        {
            CurrentEmployeeId = _employeeService.GetEmployeeIdByUserId(Session.CurrentUserId);
            if (CurrentEmployeeId is null)
                LastMessage = "?? No employee profile linked to your user. Please contact admin.";
        }

        private void SetupCommands()
        {
            // Attendance (guard on CurrentEmployeeId)
            ClockInCommand = new RelayCommand(async _ => await ClockInAsync(), _ => !IsBusyAttendance && CurrentEmployeeId.HasValue && CanExecuteAttendanceAction());
            ClockOutCommand = new RelayCommand(async _ => await ClockOutAsync(), _ => !IsBusyAttendance && CurrentEmployeeId.HasValue && CanExecuteAttendanceAction());
            RefreshListCommand = new RelayCommand(async _ => await RefreshAttendanceAsync(), _ => !IsBusyAttendance && CurrentEmployeeId.HasValue);
            ApplyFilterCommand = new RelayCommand(async _ => await RefreshAttendanceAsync(), _ => !IsBusyAttendance && CurrentEmployeeId.HasValue && CanApplyAttendanceFilter());
            ResetFilterCommand = new RelayCommand(_ => ResetAttendanceFilter(), _ => !IsBusyAttendance);

            // Leave
            LoadLeaveListCommand = new RelayCommand(async _ => await RefreshLeaveListAsync(), _ => !IsBusyLeave && CurrentEmployeeId.HasValue);
            ApplyLeaveFilterCommand = new RelayCommand(async _ => await RefreshLeaveListAsync(), _ => !IsBusyLeave && CurrentEmployeeId.HasValue && CanApplyLeaveFilter());
            ResetLeaveFilterCommand = new RelayCommand(_ => ResetLeaveFilter(), _ => !IsBusyLeave);

            NewLeaveFormCommand = new RelayCommand(_ => ResetLeaveForm(), _ => !IsBusyLeave && CurrentEmployeeId.HasValue);
            SubmitLeaveRequestCommand = new RelayCommand(async _ => await SubmitLeaveAsync(), _ => !IsBusyLeave && CurrentEmployeeId.HasValue && CanSubmitLeave());
            UpdateLeaveRequestCommand = new RelayCommand(async _ => await UpdateLeaveAsync(), _ => !IsBusyLeave && CurrentEmployeeId.HasValue && CanUpdateLeave());
            CancelLeaveRequestCommand = new RelayCommand(async _ => await CancelLeaveAsync(), _ => !IsBusyLeave && CurrentEmployeeId.HasValue && CanCancelLeave());
            EditSelectedLeaveCommand = new RelayCommand(_ => LoadSelectedIntoForm(), _ => SelectedLeave != null && !IsBusyLeave);
        }

        private void KickOffInitialLoads()
        {
            _ = LoadEmployeeHeaderAsync();
            _ = RefreshAttendanceAsync();
            _ = RefreshLeaveListAsync();
        }

        private async Task LoadEmployeeHeaderAsync()
        {
            try
            {
                if (CurrentEmployeeId is null) return;

                // Fetch name + user id (via EmployeeService)
                var empId = CurrentEmployeeId.Value;

                // These methods are expected to be provided by EmployeeService.
                // If they return null/empty, EmployeeDisplay will gracefully degrade.
                EmployeeName = await Task.Run(() => _employeeService.GetEmployeeFullName(empId) ?? string.Empty);
                UserId = await Task.Run(() => _employeeService.GetUserIdByEmployeeId(empId));
            }
            catch
            {
                // Non-fatal; UI can still function without header.
            }
        }

        // =========================================================================================
        // ATTENDANCE LOGIC
        // =========================================================================================
        private bool CanExecuteAttendanceAction()
        {
            if (!IsManualMode) return true;
            return TryGetManualDateTime(out _);
        }

        private bool TryGetManualDateTime(out DateTime result)
        {
            result = DateTime.Now;
            if (!IsManualMode) return true;
            if (ManualDate is null) return false;

            var formats = new[] { "HH:mm", "HH:mm:ss" };
            if (!DateTime.TryParseExact(ManualTimeText ?? string.Empty, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var t))
                return false;

            var d = ManualDate.Value.Date;
            result = new DateTime(d.Year, d.Month, d.Day, t.Hour, t.Minute, t.Second);
            return true;
        }

        private async Task ClockInAsync()
        {
            if (CurrentEmployeeId is null) { LastMessage = "? No linked employee profile."; return; }

            try
            {
                IsBusyAttendance = true;
                var empId = CurrentEmployeeId.Value;

                if (IsManualMode && TryGetManualDateTime(out var ts))
                    _attendanceService.ClockIn(empId, ts);   // <-- EMPLOYEE ID
                else
                    _attendanceService.ClockIn(empId);        // <-- EMPLOYEE ID

                LastMessage = $"? Clock In successful! ({DateTime.Now:hh:mm:ss tt})";
                await RefreshAttendanceAsync();
            }
            catch (Exception ex)
            {
                LastMessage = $"? Clock In failed: {ex.Message}";
            }
            finally { IsBusyAttendance = false; _ = ClearMessageAfterDelay(); }
        }

        private async Task ClockOutAsync()
        {
            if (CurrentEmployeeId is null) { LastMessage = "? No linked employee profile."; return; }

            try
            {
                IsBusyAttendance = true;
                var empId = CurrentEmployeeId.Value;

                if (IsManualMode && TryGetManualDateTime(out var ts))
                    _attendanceService.ClockOut(empId, ts);   // <-- EMPLOYEE ID
                else
                    _attendanceService.ClockOut(empId);        // <-- EMPLOYEE ID

                LastMessage = $"? Clock Out successful! ({DateTime.Now:hh:mm:ss tt})";
                await RefreshAttendanceAsync();
            }
            catch (Exception ex)
            {
                LastMessage = $"? Clock Out failed: {ex.Message}";
            }
            finally { IsBusyAttendance = false; _ = ClearMessageAfterDelay(); }
        }

        private bool CanApplyAttendanceFilter()
        {
            if (FilterFromDate.HasValue && FilterToDate.HasValue)
                return FilterFromDate.Value.Date <= FilterToDate.Value.Date;
            return true;
        }

        private void ResetAttendanceFilter()
        {
            FilterFromDate = DateTime.Today.AddDays(-30);
            FilterToDate = DateTime.Today;
            _ = RefreshAttendanceAsync();
        }

        private async Task RefreshAttendanceAsync()
        {
            try
            {
                IsBusyAttendance = true;

                if (CurrentEmployeeId is null)
                {
                    Attendances.Clear();
                    LastMessage = "?? No linked employee profile.";
                    return;
                }

                var empId = CurrentEmployeeId.Value;

                // Get raw logs in range
                var from = (FilterFromDate ?? DateTime.Today.AddDays(-30)).Date;
                var to = (FilterToDate ?? DateTime.Today).Date;
                if (to < from) (from, to) = (to, from);

                var rawLogs = await Task.Run(() =>
                    _attendanceService.GetAttendancesForEmployee(empId, from, to));

                // Group logs per day
                var byDate = rawLogs
                    .GroupBy(a => a.Date.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Obtain work schedule days_mask via EmployeeService (nullable => assume all days workdays)
                int? daysMask = null;
                try { daysMask = _employeeService.GetWorkScheduleDaysMask(empId); } catch { /* optional */ }

                // Build calendar rows day-by-day
                Attendances.Clear();

                for (var d = from; d <= to; d = d.AddDays(1))
                {
                    var isWorkday = IsScheduledWorkday(daysMask, d);
                    var hasLogs = byDate.TryGetValue(d, out var logsForDay);

                    TimeSpan? timeIn = null;
                    TimeSpan? timeOut = null;
                    string displayStatus;

                    if (hasLogs && logsForDay != null)
                    {
                        // Consider ONLY complete pairs (both in & out) for "Present"
                        var complete = logsForDay
                            .Where(x => x.TimeIn.HasValue && x.TimeOut.HasValue)
                            .ToList();

                        if (complete.Any())
                        {
                            timeIn = complete.Min(x => x.TimeIn!.Value);
                            timeOut = complete.Max(x => x.TimeOut!.Value);
                            displayStatus = "Present";
                        }
                        else
                        {
                            // Incomplete / no valid pair -> treat as missing record on a workday
                            displayStatus = isWorkday ? "No Record" : "Day Off";
                        }
                    }
                    else
                    {
                        displayStatus = isWorkday ? "No Record" : "Day Off";
                    }

                    Attendances.Add(new AttendanceDayRow
                    {
                        EmployeeDisplay = EmployeeDisplay,
                        Date = d,
                        TimeIn = timeIn,
                        TimeOut = timeOut,
                        DisplayStatus = displayStatus
                    });
                }

                // Payroll-friendly worked days (complete pairs only)
                var workedDays = await Task.Run(() => _attendanceService.GetWorkedDaysCount(empId, from, to));
                LastMessage = $"?? Loaded {Attendances.Count} day(s). ? Worked days (complete pairs only): {workedDays}.";
            }
            catch (Exception ex)
            {
                LastMessage = $"? Failed to load attendance: {ex.Message}";
            }
            finally { IsBusyAttendance = false; }
        }

        // =========================================================================================
        // LEAVE LOGIC (unchanged except for guarding with CurrentEmployeeId)
        // =========================================================================================
        private bool CanApplyLeaveFilter()
        {
            if (LeaveFilterFrom.HasValue && LeaveFilterTo.HasValue)
                return LeaveFilterFrom.Value.Date <= LeaveFilterTo.Value.Date;
            return true;
        }

        private void ResetLeaveFilter()
        {
            LeaveFilterFrom = DateTime.Today.AddDays(-60);
            LeaveFilterTo = DateTime.Today.AddDays(60);
            LeaveFilterStatus = null; // All
            _ = RefreshLeaveListAsync();
        }

        private async Task RefreshLeaveListAsync()
        {
            try
            {
                IsBusyLeave = true;

                if (CurrentEmployeeId is null) { LeaveRequests.Clear(); return; }

                var list = await Task.Run(() =>
                    _leaveService.GetForEmployee(CurrentEmployeeId.Value, LeaveFilterFrom, LeaveFilterTo, LeaveFilterStatus));

                LeaveRequests.Clear();
                foreach (var lr in list) LeaveRequests.Add(lr);

                LastMessage = $"?? Loaded {LeaveRequests.Count} leave request(s).";
            }
            catch (Exception ex)
            {
                LastMessage = $"? Failed to load leaves: {ex.Message}";
            }
            finally { IsBusyLeave = false; }
        }

        private void ResetLeaveForm()
        {
            LeaveFormId = 0;
            LeaveType = LeaveType.Sick;
            LeaveReason = string.Empty;
            LeaveDateFrom = DateTime.Today;
            LeaveDateTo = DateTime.Today;
            LeaveHalfDay = false;
            OnPropertyChanged(nameof(LeaveDurationDays));
        }

        private void LoadSelectedIntoForm()
        {
            if (SelectedLeave == null) return;

            LeaveFormId = SelectedLeave.Id;
            LeaveType = SelectedLeave.LeaveType;
            LeaveReason = SelectedLeave.Reason;
            LeaveDateFrom = SelectedLeave.DateFrom.Date;
            LeaveDateTo = SelectedLeave.DateTo.Date;
            LeaveHalfDay = SelectedLeave.HalfDay;
            OnPropertyChanged(nameof(LeaveDurationDays));
        }

        private bool CanSubmitLeave()
        {
            if (LeaveFormId != 0) return false; // new only
            if (LeaveDateFrom.Date > LeaveDateTo.Date) return false;
            if (LeaveHalfDay && LeaveDateFrom.Date != LeaveDateTo.Date) return false;
            return CurrentEmployeeId.HasValue;
        }

        private async Task SubmitLeaveAsync()
        {
            try
            {
                IsBusyLeave = true;

                if (CurrentEmployeeId is null) { LastMessage = "? No linked employee profile."; return; }

                var overlaps = await Task.Run(() =>
                    _leaveService.GetForEmployee(CurrentEmployeeId.Value, LeaveDateFrom, LeaveDateTo, null)
                                 .Any(x => x.Status == LeaveStatus.Approved || x.Status == LeaveStatus.Pending));
                if (overlaps) { LastMessage = "?? Overlaps with existing pending/approved leave."; return; }

                var model = new LeaveRequestModel
                {
                    EmployeeId = CurrentEmployeeId.Value,
                    LeaveType = LeaveType,
                    Reason = string.IsNullOrWhiteSpace(LeaveReason) ? null : LeaveReason.Trim(),
                    DateFrom = LeaveDateFrom.Date,
                    DateTo = LeaveDateTo.Date,
                    HalfDay = LeaveHalfDay,
                    Status = LeaveStatus.Pending
                };

                var id = await Task.Run(() => _leaveService.Create(model));
                LeaveFormId = id;

                LastMessage = "? Leave request submitted (Pending).";
                await RefreshLeaveListAsync();
            }
            catch (Exception ex)
            {
                LastMessage = $"? Submit failed: {ex.Message}";
            }
            finally { IsBusyLeave = false; _ = ClearMessageAfterDelay(); }
        }

        private bool CanUpdateLeave()
        {
            if (LeaveFormId <= 0) return false;
            var row = LeaveRequests.FirstOrDefault(x => x.Id == LeaveFormId);
            if (row == null || row.Status != LeaveStatus.Pending) return false;
            if (LeaveDateFrom.Date > LeaveDateTo.Date) return false;
            if (LeaveHalfDay && LeaveDateFrom.Date != LeaveDateTo.Date) return false;
            return true;
        }

        private async Task UpdateLeaveAsync()
        {
            try
            {
                IsBusyLeave = true;

                var row = LeaveRequests.FirstOrDefault(x => x.Id == LeaveFormId);
                if (row == null) { LastMessage = "?? Leave request not found."; return; }
                if (row.Status != LeaveStatus.Pending) { LastMessage = "?? Only pending requests can be edited."; return; }

                row.LeaveType = LeaveType;
                row.Reason = string.IsNullOrWhiteSpace(LeaveReason) ? null : LeaveReason.Trim();
                row.DateFrom = LeaveDateFrom.Date;
                row.DateTo = LeaveDateTo.Date;
                row.HalfDay = LeaveHalfDay;

                var ok = await Task.Run(() => _leaveService.Update(row));
                LastMessage = ok ? "? Leave request updated." : "? Update failed.";
                await RefreshLeaveListAsync();
            }
            catch (Exception ex)
            {
                LastMessage = $"? Update failed: {ex.Message}";
            }
            finally { IsBusyLeave = false; _ = ClearMessageAfterDelay(); }
        }

        private bool CanCancelLeave()
        {
            var row = SelectedLeave ?? LeaveRequests.FirstOrDefault(x => x.Id == LeaveFormId);
            return row != null && row.Status != LeaveStatus.Cancelled;
        }

        private async Task CancelLeaveAsync()
        {
            try
            {
                IsBusyLeave = true;

                var row = SelectedLeave ?? LeaveRequests.FirstOrDefault(x => x.Id == LeaveFormId);
                if (row == null) { LastMessage = "?? Select a leave request to cancel."; return; }

                var ok = await Task.Run(() => _leaveService.Cancel(row.Id));
                LastMessage = ok ? "? Leave request cancelled." : "? Cancel failed.";
                await RefreshLeaveListAsync();

                if (row.Id == LeaveFormId)
                {
                    var refreshed = LeaveRequests.FirstOrDefault(x => x.Id == LeaveFormId);
                    if (refreshed != null)
                    {
                        LeaveType = refreshed.LeaveType;
                        LeaveReason = refreshed.Reason;
                        LeaveDateFrom = refreshed.DateFrom.Date;
                        LeaveDateTo = refreshed.DateTo.Date;
                        LeaveHalfDay = refreshed.HalfDay;
                        OnPropertyChanged(nameof(LeaveDurationDays));
                    }
                }
            }
            catch (Exception ex)
            {
                LastMessage = $"? Cancel failed: {ex.Message}";
            }
            finally { IsBusyLeave = false; _ = ClearMessageAfterDelay(); }
        }

        // ===== Helpers =====
        private async Task ClearMessageAfterDelay(int ms = 4000)
        {
            try { await Task.Delay(ms); LastMessage = string.Empty; } catch { }
        }

        private static int DayOfWeekToBit(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday => 0,
            DayOfWeek.Tuesday => 1,
            DayOfWeek.Wednesday => 2,
            DayOfWeek.Thursday => 3,
            DayOfWeek.Friday => 4,
            DayOfWeek.Saturday => 5,
            DayOfWeek.Sunday => 6,
            _ => 0
        };

        private static bool IsScheduledWorkday(int? daysMask, DateTime date)
        {
            if (!daysMask.HasValue) return true; // fallback: assume workday if schedule not available
            var bit = DayOfWeekToBit(date.DayOfWeek);
            return (daysMask.Value & (1 << bit)) != 0;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    /// <summary>
    /// UI row used by Attendance grid (per day).
    /// </summary>
    public sealed class AttendanceDayRow
    {
        public string? EmployeeDisplay { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan? TimeIn { get; set; }
        public TimeSpan? TimeOut { get; set; }
        public string DisplayStatus { get; set; } = "No Record";
    }
}
