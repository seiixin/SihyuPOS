using System;

namespace SihyuPOSPayroll.Models
{
    public enum ReportPeriod { Daily, Weekly, Monthly, Quarterly, Yearly }

    public class SalesRow
    {
        public string Period { get; set; } = "";      // e.g., "2025-08-31", "2025 W35", "2025-08", "2025 Q3", "2025"
        public DateTime StartDate { get; set; }       // period start
        public DateTime EndDate { get; set; }         // period end
        public int ReceiptCount { get; set; }         // number of receipts
        public decimal TotalAmount { get; set; }      // sum of amount_paid
    }
}
