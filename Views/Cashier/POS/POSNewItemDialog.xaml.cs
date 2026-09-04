using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.Views.Admin.Inventory;

namespace SihyuPOSPayroll.Views.Cashier.POS
{
    /// <summary>
    /// Shown by the POS page when a scanned barcode doesn't exist in inventory.
    /// On save, persists the item to inventory_items and raises <see cref="DialogClosed"/>
    /// with the newly-created <see cref="InventoryItem"/> so the caller can reload
    /// the product list and auto-add it to the cart.
    /// </summary>
    public partial class POSNewItemDialog : UserControl
    {
        // ── Output ───────────────────────────────────────────────────────────
        /// <summary>
        /// Raised when the dialog closes.
        /// <c>e.Item</c> is the saved <see cref="InventoryItem"/> (non-null on success),
        /// or <c>null</c> when the user cancelled.
        /// </summary>
        public event EventHandler<InventoryItem?>? DialogClosed;

        private readonly InventoryService _inventoryService = new();

        // ── Constructor ───────────────────────────────────────────────────────
        public POSNewItemDialog(string barcode)
        {
            InitializeComponent();

            BarcodeBox.Text  = barcode;
            QuantityBox.Text = "0";
            PriceBox.Text    = ".00";

            PreviewKeyDown += Dialog_PreviewKeyDown;

            Loaded += (_, __) =>
            {
                LoadCategories();
                RunBarcodeLookupAsync(barcode);
            };
        }

        // ── Keyboard shortcuts ─────────────────────────────────────────────────
        private void Dialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    if (Keyboard.FocusedElement is ComboBox || Keyboard.FocusedElement is DatePicker)
                        return;
                    e.Handled = true;
                    Save_Click(this, new RoutedEventArgs());
                    break;

                case Key.Escape:
                    e.Handled = true;
                    Cancel_Click(this, new RoutedEventArgs());
                    break;
            }
        }

        // ── Category list ─────────────────────────────────────────────────────
        private void LoadCategories()
        {
            try
            {
                CategoryBox.ItemsSource = CategoryService.GetNames();
            }
            catch { /* leave empty, user can type */ }
        }

        // ── Barcode lookup (Local JSON → Open Food Facts) ──────────────────────
        private async void RunBarcodeLookupAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            // ── Step 1: instant local JSON ────────────────────────────────────
            var localHit = BarcodeLookupService.Lookup(barcode);
            if (localHit != null)
            {
                ProductNameBox.Text = localHit.ProductName;

                string? cat = AddEditInventoryDialog.GuessCategory(localHit.Brand, localHit.ProductName);
                if (!string.IsNullOrEmpty(cat))
                    CategoryBox.Text = cat;

                SetLookupStatus($"✓ Found: {localHit.ProductName}", isSearching: false, isSuccess: true);
                PriceBox.Focus();
                return;
            }

            // ── Step 2: Open Food Facts API ────────────────────────────────────
            SetLookupStatus("Searching Open Food Facts…", isSearching: true);

            OFFLookupResult result;
            try
            {
                result = await OpenFoodFactsService.LookupAsync(barcode).ConfigureAwait(true);
            }
            catch
            {
                result = OFFLookupResult.ErrorResult;
            }

            if (result.IsFound)
            {
                ProductNameBox.Text = result.ProductName ?? string.Empty;

                if (string.IsNullOrWhiteSpace(CategoryBox.Text))
                {
                    string? cat = result.Category
                        ?? AddEditInventoryDialog.GuessCategory(result.Brand, result.ProductName ?? string.Empty);
                    if (!string.IsNullOrEmpty(cat))
                        CategoryBox.Text = cat;
                }

                SetLookupStatus($"✓ Found: {result.ProductName}", isSearching: false, isSuccess: true);
            }
            else
            {
                string hint = result.Status switch
                {
                    OFFLookupResult.LookupStatus.Offline  => "Offline — enter product name manually",
                    OFFLookupResult.LookupStatus.Timeout  => "Lookup timed out — enter manually",
                    OFFLookupResult.LookupStatus.NotFound => "Not found — enter product name manually",
                    _                                     => "Not found — enter product name manually",
                };
                SetLookupStatus(hint, isSearching: false, isSuccess: false);
            }

            PriceBox.Focus();
        }

        /// <summary>Updates the lookup status row below the barcode field.</summary>
        private void SetLookupStatus(string message, bool isSearching, bool isSuccess = false)
        {
            LookupStatusText.Text       = message;
            LookupStatusText.Foreground = isSuccess
                ? (Brush)new BrushConverter().ConvertFrom("#4ADE80")!
                : (Brush)new BrushConverter().ConvertFrom("#9CA3AF")!;
            LookupSpinner.Visibility    = isSearching ? Visibility.Visible : Visibility.Collapsed;
            LookupStatusRow.Visibility  = string.IsNullOrEmpty(message) && !isSearching
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // ── Input validation ──────────────────────────────────────────────────
        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = Regex.IsMatch(e.Text, "[^0-9]");

        private void PriceOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = (TextBox)sender;
            string candidate = tb.Text.Insert(tb.CaretIndex, e.Text);
            e.Handled = !Regex.IsMatch(candidate, @"^\d{0,10}(\.\d{0,2})?$");
        }

        private void PriceBox_GotFocus(object sender, RoutedEventArgs e)
            => Dispatcher.InvokeAsync(() => ((TextBox)sender).SelectAll());

        // ── Save ──────────────────────────────────────────────────────────────
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate product name
            if (string.IsNullOrWhiteSpace(ProductNameBox.Text))
            {
                MessageBox.Show("Product Name is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ProductNameBox.Focus();
                return;
            }

            // Validate quantity
            if (!int.TryParse(QuantityBox.Text, out int qty) || qty < 0)
            {
                MessageBox.Show("Please enter a valid quantity (0 or more).", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                QuantityBox.Focus();
                return;
            }

            // Validate price — must be > 0 to be added to cart
            if (!decimal.TryParse(PriceBox.Text,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture,
                    out decimal price) || price <= 0)
            {
                MessageBox.Show("Price must be greater than 0 to add this item to the cart.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PriceBox.Focus();
                return;
            }

            var newItem = new InventoryItem
            {
                Barcode      = BarcodeBox.Text.Trim(),
                ProductName  = ProductNameBox.Text.Trim(),
                CategoryName = string.IsNullOrWhiteSpace(CategoryBox.Text)
                               ? null : CategoryBox.Text.Trim(),
                Quantity     = qty,
                Price        = price,
                ExpiryDate   = ExpiryDatePicker.SelectedDate,
            };

            try
            {
                bool ok = _inventoryService.AddItem(newItem);
                if (!ok)
                {
                    MessageBox.Show("Failed to save item to inventory.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving item: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogClosed?.Invoke(this, newItem);
        }

        // ── Cancel ────────────────────────────────────────────────────────────
        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogClosed?.Invoke(this, null);
    }
}
