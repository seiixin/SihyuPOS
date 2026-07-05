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

        public int HoursWorked => TotalDaysWorked * 8; // assuming 8 hours per day

        public decimal RatePerHour => (TotalDaysWorked * 8) == 0 ? 0 : GrossSalary / (TotalDaysWorked * 8);

        public decimal Deductions => SssDeduction + PhilhealthDeduction + PagibigDeduction + OtherDeductions;
    }
}
