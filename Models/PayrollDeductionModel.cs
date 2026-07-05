using System;

namespace SihyuPOSPayroll.Models
{
    public class PayrollDeductionModel
    {
        public int Id { get; set; }

        public int PayrollId { get; set; }

        public string? DeductionType { get; set; }

        public decimal? Amount { get; set; }

        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
