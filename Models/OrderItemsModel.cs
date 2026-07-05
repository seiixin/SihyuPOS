using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SihyuPOSPayroll.Models
{
    public class OrderItemModel
    {
        public int Id { get; set; }              // order_items.id
        public int OrderId { get; set; }         // order_items.order_id
        public int ProductId { get; set; }       // order_items.product_id  (bind this to the dropdown SelectedValue)
        public int Quantity { get; set; } = 1;   // order_items.quantity

        // For UI/CRUD convenience:
        public decimal UnitPrice { get; set; }   // order_items.unit_price (store frozen price at order time)
        public decimal Subtotal => UnitPrice * Quantity; // computed; persist if you prefer

        // Helpful for the grid/details without another call (optional, not required by DB):
        public string? ProductName { get; set; } // join from Menu when reading
        public string? Category { get; set; }    // join from Menu when reading
    }
}
