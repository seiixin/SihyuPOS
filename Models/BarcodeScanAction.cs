namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Controls what happens when an existing inventory item's barcode is scanned
    /// on the Inventory page.
    ///
    /// ModalPrompt  — (default) Opens a compact dialog with two quick-action buttons:
    ///                "Add Stock (+1)" and "Edit Product Details".
    /// QuickAdd     — Silently increments the item's stock by +1 with no modal.
    /// QuickUpdate  — Opens the full Edit modal pre-filled with the item's details.
    /// </summary>
    public enum BarcodeScanAction
    {
        ModalPrompt,
        QuickAdd,
        QuickUpdate,
    }
}
