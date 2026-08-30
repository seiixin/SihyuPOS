namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Returned by <c>SettlePaymentDialog</c> when the cashier confirms a payment.
    /// </summary>
    public class SettlePaymentResult
    {
        /// <summary>Payment method chosen by the cashier.</summary>
        public string PaymentMethod { get; set; } = "Cash";

        /// <summary>Total bill amount (from the order).</summary>
        public decimal Total { get; set; }

        /// <summary>Cash / amount physically received from the customer.</summary>
        public decimal CashReceived { get; set; }

        /// <summary>Change to return: CashReceived − Total (≥ 0).</summary>
        public decimal Change => CashReceived >= Total ? CashReceived - Total : 0m;

        /// <summary>Actual amount recorded as paid (≤ Total).</summary>
        public decimal Paid => Total;

        /// <summary>Optional cashier remarks.</summary>
        public string Remarks { get; set; } = string.Empty;

        /// <summary>True when the cashier clicked Save; false when Print was clicked.</summary>
        public bool PrintReceipt { get; set; }
    }
}
