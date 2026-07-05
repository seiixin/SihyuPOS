using System;

namespace SihyuPOSPayroll.Models
{
    public class PayslipModel
    {
        public int PayslipId { get; set; }

        public int EmployeeId { get; set; }
        public string FullName { get; set; } = string.Empty;

        public DateTime PayDate { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal RatePerHour { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetSalary { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public string Status { get; set; } = "Pending";
    }
}
