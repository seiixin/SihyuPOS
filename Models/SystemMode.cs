namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Defines the operating mode for the SihyuPOS deployment.
    /// RestaurantMode: Full inventory, POS, and menu capabilities with recipe-based ingredient tracking.
    /// StoreMode: Simplified retail inventory and POS with per-unit deduction.
    /// </summary>
    public enum SystemMode
    {
        RestaurantMode,
        StoreMode
    }
}
