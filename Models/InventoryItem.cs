using System;

namespace SihyuPOSPayroll.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }                
        public string ProductName { get; set; }    
        public string? CategoryName { get; set; }  
        public int Quantity { get; set; }          
        public DateTime? ExpiryDate { get; set; }  

        public string Status
        {
            get
            {
                if (!ExpiryDate.HasValue) return "No Expiry";
                var daysLeft = (ExpiryDate.Value - DateTime.Now).Days;
                if (daysLeft < 0) return "Expired";
                if (daysLeft <= 7) return $"Expiring Soon ({daysLeft} days)";
                return $"Fresh ({daysLeft} days)";
            }
        }
    }
}
