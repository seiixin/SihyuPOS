using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.Views.Admin.Inventory
{
    public partial class AddEditInventoryDialog : UserControl
    {
        // ── Output properties read by InventoryViewModel ───────────────────────
        public string  ProductName  { get; private set; } = string.Empty;
        public string? CategoryName { get; private set; }
        public int     Quantity     { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public string? ImagePath   { get; private set; }

        public event EventHandler<bool>? DialogClosed;

        // ── Constructors ───────────────────────────────────────────────────────
        public AddEditInventoryDialog()
        {
            InitializeComponent();
            QuantityTextBox.Text = "0";
            LoadCategories();
        }

        public AddEditInventoryDialog(InventoryItem item) : this()
        {
            DialogTitle.Text              = "Edit Product";
            ProductNameTextBox.Text       = item.ProductName;
            CategoryComboBox.Text         = item.CategoryName ?? string.Empty;
            QuantityTextBox.Text          = item.Quantity.ToString();
            ExpiryDatePicker.SelectedDate = item.ExpiryDate;
            SetImagePath(item.ImagePath);
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

            if (ExpiryDatePicker.SelectedDate.HasValue &&
                ExpiryDatePicker.SelectedDate.Value.Date < DateTime.Today)
            {
                var r = MessageBox.Show(
                    "The expiry date is in the past. Continue?",
                    "Past Expiry Date", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.No) { ExpiryDatePicker.Focus(); return; }
            }

            ProductName  = ProductNameTextBox.Text.Trim();
            CategoryName = string.IsNullOrWhiteSpace(CategoryComboBox.Text)
                           ? null : CategoryComboBox.Text.Trim();
            Quantity   = qty;
            ExpiryDate = ExpiryDatePicker.SelectedDate;
            // ImagePath already set via SetImagePath()

            DialogClosed?.Invoke(this, true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
            => DialogClosed?.Invoke(this, false);

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
            => e.Handled = Regex.IsMatch(e.Text, "[^0-9]");
    }
}
