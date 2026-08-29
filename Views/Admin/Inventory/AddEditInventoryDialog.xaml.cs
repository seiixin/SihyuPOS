using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.Views.Admin.Inventory
{
    /// <summary>
    /// Data bag passed to <see cref="AddEditInventoryDialog(QuickAddPrefill)"/> so the
    /// dialog can pre-populate fields and show the correct source badge.
    /// </summary>
    public sealed class QuickAddPrefill
    {
        public enum PrefillSource { LocalJson, OpenFoodFacts, ManualEntry }

        public string        Barcode     { get; init; } = string.Empty;
        public string?       ProductName { get; init; }
        public string?       Brand       { get; init; }
        public string?       Category    { get; init; }
        public PrefillSource Source      { get; init; } = PrefillSource.ManualEntry;
    }

    public partial class AddEditInventoryDialog : UserControl
    {
        // ── Output properties read by InventoryViewModel ───────────────────────
        public string? Barcode      { get; private set; }
        public string  ProductName  { get; private set; } = string.Empty;
        public string? CategoryName { get; private set; }
        public int     Quantity     { get; private set; }
        public decimal Price        { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public string? ImagePath    { get; private set; }

        public event EventHandler<bool>? DialogClosed;

        // ── Constructors ───────────────────────────────────────────────────────
        public AddEditInventoryDialog()
        {
            InitializeComponent();
            QuantityTextBox.Text = "0";
            PriceTextBox.Text    = "0.00";
            LoadCategories();
            Loaded += (_, __) =>
            {
                if (!string.IsNullOrWhiteSpace(BarcodeTextBox.Text))
                    ApplyJsonLookup(BarcodeTextBox.Text, overwriteBlankOnly: true);
            };
        }

        /// <summary>
        /// Quick-Add constructor — accepts a fully-resolved <see cref="QuickAddPrefill"/>
        /// and pre-fills all available fields, showing the lookup source badge.
        /// </summary>
        public AddEditInventoryDialog(QuickAddPrefill prefill) : this()
        {
            BarcodeTextBox.Text = prefill.Barcode;

            if (!string.IsNullOrWhiteSpace(prefill.ProductName))
                ProductNameTextBox.Text = prefill.ProductName;

            if (!string.IsNullOrWhiteSpace(prefill.Category))
                CategoryComboBox.Text = prefill.Category;
            else if (!string.IsNullOrWhiteSpace(prefill.Brand) || !string.IsNullOrWhiteSpace(prefill.ProductName))
            {
                string? guessed = GuessCategoryFromBrand(prefill.Brand, prefill.ProductName ?? string.Empty);
                if (!string.IsNullOrEmpty(guessed))
                    CategoryComboBox.Text = guessed;
            }

            SetSourceBadge(prefill.Source);
        }

        /// <summary>Legacy barcode-only constructor — still used by plain AddItemWithBarcode path.</summary>
        public AddEditInventoryDialog(string prefillBarcode) : this()
        {
            BarcodeTextBox.Text = prefillBarcode?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(BarcodeTextBox.Text))
                ApplyJsonLookup(BarcodeTextBox.Text, overwriteBlankOnly: true);
        }

        public AddEditInventoryDialog(InventoryItem item) : this()
        {
            DialogTitle.Text              = "Edit Product";
            BarcodeTextBox.Text           = item.Barcode ?? string.Empty;
            ProductNameTextBox.Text       = item.ProductName;
            CategoryComboBox.Text         = item.CategoryName ?? string.Empty;
            QuantityTextBox.Text          = item.Quantity.ToString();
            PriceTextBox.Text             = item.Price.ToString("0.00");
            ExpiryDatePicker.SelectedDate = item.ExpiryDate;
            SetImagePath(item.ImagePath);
        }

        // ── Source badge ───────────────────────────────────────────────────────
        private void SetSourceBadge(QuickAddPrefill.PrefillSource source)
        {
            (string label, string bg, string fg) = source switch
            {
                QuickAddPrefill.PrefillSource.LocalJson      => ("Local DB",         "#1A3A2A", "#4ADE80"),
                QuickAddPrefill.PrefillSource.OpenFoodFacts  => ("Open Food Facts",  "#1A2A3A", "#60A5FA"),
                QuickAddPrefill.PrefillSource.ManualEntry    => ("Manual Entry",     "#2A2A1A", "#FCD34D"),
                _                                            => ("",                 "#1A1A1A", "#9CA3AF"),
            };

            if (string.IsNullOrEmpty(label)) return;

            SourceBadgeText.Text       = label;
            SourceBadge.Background     = (Brush)new BrushConverter().ConvertFrom(bg)!;
            SourceBadgeText.Foreground = (Brush)new BrushConverter().ConvertFrom(fg)!;
            SourceBadge.Visibility     = Visibility.Visible;
        }

        // ── Category loader ────────────────────────────────────────────────────
        private void LoadCategories()
        {
            try
            {
                var names = CategoryService.GetNames();
                CategoryComboBox.ItemsSource = names;
            }
            catch
            {
                // if DB isn't ready yet, leave empty — user can type freely
            }
        }

        // ── JSON lookup handler (fires on barcode TextChanged) ────────────────
        private void BarcodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string barcode = BarcodeTextBox.Text.Trim();
            if (string.IsNullOrEmpty(barcode)) return;
            ApplyJsonLookup(barcode, overwriteBlankOnly: true);
        }

        private void ApplyJsonLookup(string barcode, bool overwriteBlankOnly)
        {
            var hit = BarcodeLookupService.Lookup(barcode);
            if (hit == null) return;   // silently do nothing when not found

            if (!overwriteBlankOnly || string.IsNullOrWhiteSpace(ProductNameTextBox.Text))
                ProductNameTextBox.Text = hit.ProductName;

            if (!overwriteBlankOnly || string.IsNullOrWhiteSpace(CategoryComboBox.Text))
            {
                string? bestCategory = GuessCategoryFromBrand(hit.Brand, hit.ProductName);
                if (!string.IsNullOrEmpty(bestCategory))
                    CategoryComboBox.Text = bestCategory;
            }
        }

        // ── Category guesser (public static so InventoryViewModel can call it) ──
        /// <summary>Maps brand/product-name hints to a category string. Returns null if uncertain.</summary>
        public static string? GuessCategory(string? brand, string productName) =>
            GuessCategoryFromBrand(brand, productName);

        private static string? GuessCategoryFromBrand(string? brand, string productName)
        {
            string lowBrand = (brand ?? "").ToLowerInvariant();
            string lowName  = productName.ToLowerInvariant();

            // Beverages
            if (lowBrand.Contains("nescafe") || lowBrand.Contains("kopiko")
                || lowBrand.Contains("blend 45") || lowBrand.Contains("great taste")
                || lowName.Contains("coffee") || lowName.Contains("3-in-1"))
                return "Beverages";
            if (lowBrand.Contains("coca-cola") || lowBrand.Contains("sprite") || lowBrand.Contains("royal")
                || lowBrand.Contains("pepsi") || lowBrand.Contains("mirinda") || lowBrand.Contains("mountain dew")
                || lowBrand.Contains("sarsi") || lowName.Contains("juice") || lowBrand.Contains("iced tea")
                || lowBrand.Contains("zesto") || lowBrand.Contains("lipton") || lowBrand.Contains("nestea")
                || lowBrand.Contains("c2"))
                return "Beverages";

            // Milk & Dairy
            if (lowBrand.Contains("bear brand") || lowBrand.Contains("alaska")
                || lowBrand.Contains("nido") || lowBrand.Contains("carnation")
                || lowBrand.Contains("cowhead") || lowBrand.Contains("jersey")
                || lowName.Contains("milk") || lowBrand.Contains("evaporada")
                || lowBrand.Contains("magnolia") && (lowName.Contains("milk") || lowName.Contains("chocolait")))
                return "Dairy";
            if (lowName.Contains("cheese") || lowBrand.Contains("chee") || lowName.Contains("cheeze"))
                return "Dairy";

            // Snacks / Biscuits
            if (lowBrand.Contains("rebisco") || lowBrand.Contains("hansel") || lowBrand.Contains("fita")
                || lowBrand.Contains("skyflakes") || lowBrand.Contains("m.y. san") || lowBrand.Contains("butter coconut")
                || lowBrand.Contains("pringles") || lowBrand.Contains("piattos") || lowBrand.Contains("nova")
                || lowBrand.Contains("chippy") || lowBrand.Contains("lucky curls") || lowBrand.Contains("clover")
                || lowBrand.Contains("oishi") || lowBrand.Contains("potchi") || lowBrand.Contains("dingdong")
                || lowBrand.Contains("muncher") || lowBrand.Contains("boy bawang") || lowBrand.Contains("cornick")
                || lowBrand.Contains("haw haw") || lowBrand.Contains("mikmik") || lowBrand.Contains("wiggles")
                || lowBrand.Contains("ribbon"))
                return "Snacks";

            // Noodles & Pasta
            if (lowBrand.Contains("lucky me!") || lowBrand.Contains("lucky me")
                || lowBrand.Contains("payless") || lowBrand.Contains("quickchow")
                || lowName.Contains("noodles") || lowName.Contains("pancit") || lowName.Contains("mami")
                || lowName.Contains("batchoy"))
                return "Noodles & Pasta";

            // Canned Goods
            if (lowBrand.Contains("argentina") || lowBrand.Contains("purefoods") || lowBrand.Contains("cdo")
                || lowBrand.Contains("maling") || lowBrand.Contains("spam") || lowBrand.Contains("century tuna")
                || lowBrand.Contains("san marino") || lowBrand.Contains("555") || lowBrand.Contains("ligo")
                || lowBrand.Contains("mega") || lowBrand.Contains("young's town") || lowName.Contains("sardines")
                || lowName.Contains("corned beef") || lowName.Contains("luncheon meat")
                || lowBrand.Contains("reno") || lowBrand.Contains("barrio fiesta")
                || lowBrand.Contains("rico's") || lowName.Contains("tuyo") || lowName.Contains("daing"))
                return "Canned Goods";

            // Bread & Bakery
            if (lowBrand.Contains("gardenia") || lowBrand.Contains("marby's")
                || lowBrand.Contains("goldilocks") || lowName.Contains("pianono")
                || lowName.Contains("polvoron") || lowName.Contains("pandesal")
                || lowName.Contains("monay"))
                return "Bakery";

            // Candy / Chocolate
            if (lowBrand.Contains("chocnut") || lowBrand.Contains("kitkat")
                || lowBrand.Contains("toblerone") || lowBrand.Contains("snickers")
                || lowBrand.Contains("mars") || lowBrand.Contains("twix") || lowBrand.Contains("nestle crunch"))
                return "Candy";

            // Rice
            if (lowName.Contains("rice") || lowBrand.Contains("sinandomeng")
                || lowBrand.Contains("jasmine") || lowBrand.Contains("denorado") || lowBrand.Contains("malagkit"))
                return "Rice";

            return null;
        }

        // ── Image helpers ──────────────────────────────────────────────────────
        private void SetImagePath(string? path)
        {
            ImagePath = string.IsNullOrWhiteSpace(path) ? null : path;
            ImagePathBox.Text = ImagePath ?? string.Empty;

            if (ImagePath != null && File.Exists(ImagePath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource       = new Uri(ImagePath, UriKind.Absolute);
                    bmp.CacheOption     = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 160;
                    bmp.EndInit();
                    bmp.Freeze();
                    ImagePreview.Source     = bmp;
                    ImagePreview.Visibility = Visibility.Visible;
                }
                catch
                {
                    ImagePreview.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ImagePreview.Visibility = Visibility.Collapsed;
            }
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Select Product Image",
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
                SetImagePath(dlg.FileName);
        }

        private void ClearImage_Click(object sender, RoutedEventArgs e) => SetImagePath(null);

        // ── Save / Cancel ──────────────────────────────────────────────────────
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProductNameTextBox.Text))
            {
                MessageBox.Show("Product Name is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ProductNameTextBox.Focus();
                return;
            }

            if (!int.TryParse(QuantityTextBox.Text, out int qty) || qty < 0)
            {
                MessageBox.Show("Please enter a valid quantity (0 or more).", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                QuantityTextBox.Focus();
                return;
            }

            if (!decimal.TryParse(PriceTextBox.Text,
                    System.Globalization.NumberStyles.Currency | System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture, out decimal price)
                || price < 0)
            {
                MessageBox.Show("Please enter a valid price (0.00 or more).", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PriceTextBox.Focus();
                return;
            }

            if (ExpiryDatePicker.SelectedDate.HasValue &&
                ExpiryDatePicker.SelectedDate.Value.Date < DateTime.Today)
            {
                var r = MessageBox.Show(
                    "The expiry date is in the past. Continue?",
                    "Past Expiry Date", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.No) { ExpiryDatePicker.Focus(); return; }
            }

            Barcode      = string.IsNullOrWhiteSpace(BarcodeTextBox.Text) ? null : BarcodeTextBox.Text.Trim();
            ProductName  = ProductNameTextBox.Text.Trim();
            CategoryName = string.IsNullOrWhiteSpace(CategoryComboBox.Text)
                           ? null : CategoryComboBox.Text.Trim();
            Quantity     = qty;
            Price        = price;
            ExpiryDate   = ExpiryDatePicker.SelectedDate;
            // ImagePath already set via SetImagePath()

            DialogClosed?.Invoke(this, true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
            => DialogClosed?.Invoke(this, false);

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
            => e.Handled = Regex.IsMatch(e.Text, "[^0-9]");

        private void PriceValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var tb = (TextBox)sender;
            string candidate = (tb.CaretIndex > 0 ? tb.Text.Insert(tb.CaretIndex, e.Text) : tb.Text + e.Text);
            if (string.IsNullOrEmpty(candidate) || candidate == ".") { e.Handled = false; return; }
            // Allow up to one decimal, digits only otherwise
            e.Handled = !Regex.IsMatch(candidate, @"^\d{0,10}(\.\d{0,2})?$");
        }
    }
}
