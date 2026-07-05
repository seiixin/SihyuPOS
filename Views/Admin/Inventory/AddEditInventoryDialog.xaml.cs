using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SihyuPOSPayroll.Models;

namespace SihyuPOSPayroll.Views.Admin.Inventory
{
    public partial class AddEditInventoryDialog : UserControl
    {
        public string ProductName { get; private set; }
        public string CategoryName { get; private set; }
        public int Quantity { get; private set; }
        public DateTime? ExpiryDate { get; private set; }

        // Events to notify parent container
        public event EventHandler<bool> DialogClosed; // bool = true if saved, false if canceled

        public AddEditInventoryDialog()
        {
            InitializeComponent();

            // Default values
            ExpiryDatePicker.SelectedDate = null;
            QuantityTextBox.Text = "0";
        }

        public AddEditInventoryDialog(InventoryItem item) : this()
        {
            // Populate fields with existing item data
            ProductNameTextBox.Text = item.ProductName;
            CategoryComboBox.Text = item.CategoryName ?? "";
            QuantityTextBox.Text = item.Quantity.ToString();
            ExpiryDatePicker.SelectedDate = item.ExpiryDate;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(ProductNameTextBox.Text))
            {
                MessageBox.Show("Product Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProductNameTextBox.Focus();
                return;
            }

            if (!int.TryParse(QuantityTextBox.Text, out int quantity) || quantity < 0)
            {
                MessageBox.Show("Please enter a valid quantity (0 or greater).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                QuantityTextBox.Focus();
                return;
            }

            if (ExpiryDatePicker.SelectedDate.HasValue && ExpiryDatePicker.SelectedDate.Value.Date < DateTime.Today)
            {
                var result = MessageBox.Show(
                    "The expiry date is in the past. Do you want to continue?",
                    "Past Expiry Date",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    ExpiryDatePicker.Focus();
                    return;
                }
            }

            // Set properties
            ProductName = ProductNameTextBox.Text.Trim();
            CategoryName = string.IsNullOrWhiteSpace(CategoryComboBox.Text) ? null : CategoryComboBox.Text.Trim();
            Quantity = quantity;
            ExpiryDate = ExpiryDatePicker.SelectedDate;

            // Notify parent that Save was clicked
            DialogClosed?.Invoke(this, true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Notify parent that Cancel was clicked
            DialogClosed?.Invoke(this, false);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
