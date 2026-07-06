using System;
using System.Collections.Generic;
using System.Linq;

namespace SihyuPOSPayroll.Models
{
    // Enums kept in same file (no extra files needed)
    public enum PaymentStatus
    {
        Paid,
        Unpaid
    }

    public enum OrderStatus
    {
        Pending,
        Preparing,
        Served,
        Cancelled
    }

    public class OrderModel
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public string? TableNumber { get; set; }
        public decimal TotalAmount { get; set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
        public DateTime CreatedAt { get; set; }
        public int? CashRegisterId { get; set; }
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;
        public int? OrderedByUserId { get; set; }
        public OrderType OrderType { get; set; } = OrderType.NotApplicable;

        // Navigation: list of order items
        public List<OrderItemModel> Items { get; set; } = new();

        // Helper: recalc total based on items
        public void RecalculateTotal()
        {
            TotalAmount = Items.Sum(i => i.Subtotal);
        }
    }
}
