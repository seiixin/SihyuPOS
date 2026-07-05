#nullable enable
using System;
using System.ComponentModel;

namespace SihyuPOSPayroll.Models
{
    public enum LeaveStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }

    public enum LeaveType
    {
        Sick,
        Vacation,
        Emergency,
        Unpaid,
        Other
    }

    /// <summary>
    /// DTO for a leave request. LeaveType/LeaveStatus are stored as VARCHAR in DB and mapped to enums in code.
    /// Adds EmployeeName and a convenience EmployeeDisplay property for UI (e.g., "Jane Dela Cruz (ID 12)").
    /// </summary>
    public sealed class LeaveRequestModel : INotifyPropertyChanged
    {
        private int _employeeId;
        private string? _employeeName;

        public int Id { get; set; }

        /// <summary>FK ? employees.id</summary>
        public int EmployeeId
        {
            get => _employeeId;
            set
            {
                if (value == _employeeId) return;
                _employeeId = value;
                OnPropertyChanged(nameof(EmployeeId));
                // Also notify that the computed display changed
                OnPropertyChanged(nameof(EmployeeDisplay));
            }
        }

        /// <summary>Employee full name (joined from employees table).</summary>
        public string? EmployeeName
        {
            get => _employeeName;
            set
            {
                if (value == _employeeName) return;
                _employeeName = value;
                OnPropertyChanged(nameof(EmployeeName));
                // Also notify that the computed display changed
                OnPropertyChanged(nameof(EmployeeDisplay));
            }
        }

        public LeaveType LeaveType { get; set; }      // maps to VARCHAR in DB
        public string? Reason { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public bool HalfDay { get; set; }
        public LeaveStatus Status { get; set; }       // maps to VARCHAR in DB
        public int? ApproverUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Read-only helper for the grid/UI.
        /// Returns "EmployeeName (ID {EmployeeId})" when name is available, else "ID {EmployeeId}".
        /// </summary>
        public string EmployeeDisplay =>
            string.IsNullOrWhiteSpace(EmployeeName)
                ? $"ID {EmployeeId}"
                : $"{EmployeeName} (ID {EmployeeId})";

        // -------------- INotifyPropertyChanged --------------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
