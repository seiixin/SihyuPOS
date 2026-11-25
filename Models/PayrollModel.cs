using System;

namespace SihyuPOSPayroll.Models
{
    public class PayrollModel
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int TotalDaysWorked { get; set; }

        public decimal GrossSalary { get; set; }

        public decimal SssDeduction { get; set; }

        public decimal PhilhealthDeduction { get; set; }

        public decimal PagibigDeduction { get; set; }

        public decimal OtherDeductions { get; set; }

        public decimal Bonus { get; set; }

        public decimal NetSalary { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string ShiftType { get; set; } = string.Empty; // Enum: "Day" or "Night"

        public string EmployeeFullName { get; set; } = string.Empty;

        // Computed properties for UI convenience

        public string PayDate => EndDate.ToShortDateString();

        /// <summary>
        /// BUG 16 FIX: HoursWorked previously hardcoded 8 h/day regardless of shift type.
        /// Now uses ShiftType to determine hours: Night shifts are 10 h, all others default
        /// to 8 h. The constant is exposed so callers can override if needed.
        /// </summary>
        public const int DefaultHoursPerDay = 8;
        public const int NightShiftHoursPerDay = 10;

        public int HoursPerDay =>
            string.Equals(ShiftType?.Trim(), "Night", StringComparison.OrdinalIgnoreCase)
                ? NightShiftHoursPerDay
                : DefaultHoursPerDay;

        public int HoursWorked => TotalDaysWorked * HoursPerDay;

        public decimal RatePerHour =>
            HoursWorked == 0 ? 0 : GrossSalary / HoursWorked;

        public decimal Deductions => SssDeduction + PhilhealthDeduction + PagibigDeduction + OtherDeductions;
    }
}
