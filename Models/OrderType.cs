namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Classifies the order type for restaurant mode operations.
    /// Used by the DeductionEngine to determine whether packaging materials
    /// should be deducted in addition to core recipe ingredients.
    /// </summary>
    public enum OrderType
    {
        /// <summary>
        /// No order type classification applicable (e.g., StoreMode orders).
        /// </summary>
        NotApplicable,

        /// <summary>
        /// The customer is dining in; only recipe ingredients are deducted.
        /// </summary>
        DineIn,

        /// <summary>
        /// The customer is taking out; recipe ingredients and packaging materials are deducted.
        /// </summary>
        TakeOut
    }
}
