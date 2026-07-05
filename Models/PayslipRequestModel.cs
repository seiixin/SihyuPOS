using System;

namespace SihyuPOSPayroll.Models
{
    public class PayslipRequestModel
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int PayrollId { get; set; }
        public DateTime RequestDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }

        public DateTime? UpdatedDate { get; set; }
        public string FullName { get; set; } = string.Empty;
    }
}