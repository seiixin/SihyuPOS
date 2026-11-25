    namespace SihyuPOSPayroll.Models
    {
        public class ReceiptsModel
        {
            public int     ReceiptId   { get; set; }
            public int     OrderId     { get; set; }
            // BUG 14 FIX: table numbers can be non-numeric (e.g. "A1", "VIP-1").
            // Storing as string? preserves them exactly instead of silently mapping
            // any non-integer value to 0.
            public string? TableNumber { get; set; }
            public string  Date        { get; set; } = string.Empty;
            public decimal Amount      { get; set; }
        }
    }
