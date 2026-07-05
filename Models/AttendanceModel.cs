#nullable enable
using System;

namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// DB-mapped attendance row (raw logs).
    /// </summary>
    public sealed class AttendanceModel
    {
        public int Id { get; set; }

        /// <summary>FK ? employees.id</summary>
        public int EmployeeId { get; set; }

        /// <summary>
        /// The calendar date of the shift (date-only; time component should be 00:00:00).
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>MySQL TIME ? C# TimeSpan</summary>
        public TimeSpan? TimeIn { get; set; }

        /// <summary>MySQL TIME ? C# TimeSpan</summary>
        public TimeSpan? TimeOut { get; set; }

        /// <summary>
        /// Optional raw status label from DB (e.g., "Present"). UI should prefer DisplayStatus below.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>Optional audit field if your schema stores it.</summary>
        public DateTime? CreatedAt { get; set; }

        // ---------- Convenience / computed (safe for UI) ----------

        /// <summary>True when BOTH time_in AND time_out are present (a completed shift).</summary>
        public bool HasCompleteShift => TimeIn.HasValue && TimeOut.HasValue;

        /// <summary>Date + TimeIn (if set), else null.</summary>
        public DateTime? DateTimeIn => TimeIn.HasValue ? Date.Date.Add(TimeIn.Value) : (DateTime?)null;

        /// <summary>Date + TimeOut (if set), else null.</summary>
        public DateTime? DateTimeOut => TimeOut.HasValue ? Date.Date.Add(TimeOut.Value) : (DateTime?)null;

        /// <summary>
        /// Total hours worked for the row (0 if incomplete or invalid). Does not handle overnight cross-date shifts.
        /// </summary>
        public double HoursWorked =>
            (HasCompleteShift && DateTimeOut > DateTimeIn)
                ? (DateTimeOut!.Value - DateTimeIn!.Value).TotalHours
                : 0.0;

        // ---- UI helpers (not persisted) ----

        /// <summary>
        /// Whether this calendar date is part of the employee's work schedule.
        /// Set by services/VM when constructing per-day calendars.
        /// </summary>
        public bool? IsWorkday { get; set; }

        /// <summary>
        /// Employee name for display (optional; filled by JOINs/VM).
        /// </summary>
        public string? EmployeeName { get; set; }

        /// <summary>
        /// Linked users.id for display (optional; filled by JOINs/VM).
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// "Name (User #123)" convenience for headers.
        /// </summary>
        public string EmployeeDisplay =>
            $"{(string.IsNullOrWhiteSpace(EmployeeName) ? "Unknown" : EmployeeName)}" +
            $"{(UserId.HasValue ? $" (User #{UserId})" : " (No Login)")}";

        /// <summary>
        /// Computed display status for UI:
        /// - "Day Off"   when IsWorkday == false
        /// - "Present"   only when BOTH TimeIn and TimeOut exist
        /// - "No Record" when IsWorkday == true and both logs are missing
        /// - "—"         for any other partial/in-progress case or unknown workday
        /// </summary>
        public string DisplayStatus
        {
            get
            {
                if (IsWorkday == false) return "Day Off";
                var hasIn = TimeIn.HasValue;
                var hasOut = TimeOut.HasValue;
                if (hasIn && hasOut) return "Present";
                if (IsWorkday == true && !hasIn && !hasOut) return "No Record";
                return "—";
            }
        }
    }

    /// <summary>
    /// Per-day view model/DTO for the employee Attendance grid and calendar views.
    /// Not a DB entity. This is what GetDailyAttendanceCalendar(...) returns.
    /// </summary>
    public sealed class AttendanceDayModel
    {
        /// <summary>FK ? employees.id</summary>
        public int EmployeeId { get; set; }

        public string? EmployeeName { get; set; }
        public int? UserId { get; set; }
        public string EmployeeDisplay =>
            $"{(string.IsNullOrWhiteSpace(EmployeeName) ? "Unknown" : EmployeeName)}" +
            $"{(UserId.HasValue ? $" (User #{UserId})" : " (No Login)")}";

        /// <summary>Calendar date represented by this row.</summary>
        public DateTime Date { get; set; }

        public TimeSpan? TimeIn { get; set; }
        public TimeSpan? TimeOut { get; set; }

        /// <summary>True if scheduled to work on this date (from work_schedule.days_mask).</summary>
        public bool IsWorkday { get; set; }

        public DateTime? DateTimeIn => TimeIn.HasValue ? Date.Date.Add(TimeIn.Value) : (DateTime?)null;
        public DateTime? DateTimeOut => TimeOut.HasValue ? Date.Date.Add(TimeOut.Value) : (DateTime?)null;

        public bool HasCompleteShift => TimeIn.HasValue && TimeOut.HasValue;

        public double HoursWorked =>
            (HasCompleteShift && DateTimeOut > DateTimeIn)
                ? (DateTimeOut!.Value - DateTimeIn!.Value).TotalHours
                : 0.0;

        /// <summary>
        /// Display status derived from schedule + logs.
        /// </summary>
        public string DisplayStatus
        {
            get
            {
                if (!IsWorkday) return "Day Off";
                var hasIn = TimeIn.HasValue;
                var hasOut = TimeOut.HasValue;
                if (hasIn && hasOut) return "Present";
                if (!hasIn && !hasOut) return "No Record";
                return "—";
            }
        }
    }
}
