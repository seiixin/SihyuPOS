using System.Windows;
using System.Windows.Controls;
using SihyuPOSPayroll.Models;

namespace SihyuPOSPayroll.Views.Admin.Inventory
{
    /// <summary>
    /// Compact overlay shown when a known barcode is scanned and BarcodeScanAction
    /// is set to <c>ModalPrompt</c> (the default).
    ///
    /// Raises <see cref="ActionChosen"/> with one of:
    ///   BarcodeDialogAction.AddStock  — caller should increment qty by +1
    ///   BarcodeDialogAction.Edit      — caller should open the full Edit modal
    ///   BarcodeDialogAction.Cancel    — user dismissed the dialog
    /// </summary>
    public partial class BarcodeActionDialog : UserControl
    {
        // ── Public action result ───────────────────────────────────────────────
        public enum BarcodeDialogAction { AddStock, Edit, Cancel }
        public event System.EventHandler<BarcodeDialogAction>? ActionChosen;

        // ── Bound item ────────────────────────────────────────────────────────
        public InventoryItem? Item { get; private set; }

        // ── Constructor ───────────────────────────────────────────────────────
        public BarcodeActionDialog(InventoryItem item)
        {
            InitializeComponent();
            Item = item;

            ProductNameText.Text = item.ProductName;
            CategoryText.Text    = string.IsNullOrWhiteSpace(item.CategoryName) ? "No category" : item.CategoryName;
            StockText.Text       = item.Quantity.ToString();
            PriceText.Text       = item.Price.ToString("N2");
        }

        // ── Button handlers ───────────────────────────────────────────────────
        private void AddStock_Click(object sender, RoutedEventArgs e)
            => ActionChosen?.Invoke(this, BarcodeDialogAction.AddStock);

        private void EditDetails_Click(object sender, RoutedEventArgs e)
            => ActionChosen?.Invoke(this, BarcodeDialogAction.Edit);

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => ActionChosen?.Invoke(this, BarcodeDialogAction.Cancel);
    }
}
