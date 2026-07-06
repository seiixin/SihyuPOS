namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Represents a low-stock notification raised by the DeductionEngine when an inventory
    /// item's remaining quantity falls below the configured threshold after an order deduction.
    /// </summary>
    /// <param name="InventoryItemId">The primary key of the affected inventory_items record.</param>
    /// <param name="ProductName">The display name of the product or ingredient.</param>
    /// <param name="RemainingQuantity">
    /// The quantity remaining after the deduction.
    /// A value of -1 is used as a sentinel meaning "recipe data missing" for restaurant mode.
    /// </param>
    public record LowStockAlert(int InventoryItemId, string ProductName, int RemainingQuantity);
}
