    namespace SihyuPOSPayroll.Models
    {
        public class ReceiptsModel
        {
            public int ReceiptId { get; set; }
            public int OrderId { get; set; }
            public int TableNumber { get; set; }
            public string Date { get; set; } 
            public decimal Amount { get; set; }
        }
    }
