using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Cashier.POS
{
    public partial class POSView : UserControl
    {
        private OrdersViewModel Vm => (OrdersViewModel)DataContext;

        // filtered list for left product pane
        private readonly List<MenuModel> _filteredMenu = new();

        public POSView()
        {
            InitializeComponent();

            try
            {
                DataContext = new OrdersViewModel();   // reuse admin VM
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize POS ViewModel.\n\n{ex}",
                    "POS", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Loaded += POSView_Loaded;
        }

        private void POSView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Vm.BeginAdd(); // start a fresh order

                // Bind product list
                ProductList.ItemsSource = _filteredMenu;

                // Build categories from menu
                var cats = Vm.MenuProducts
                             .Select(m => m.Category ?? "Uncategorized")
                             .Distinct()
                             .OrderBy(c => c)
                             .ToList();
                cats.Insert(0, "All");
                CategoryCombo.ItemsSource = cats;
                if (CategoryCombo.Items.Count > 0) CategoryCombo.SelectedIndex = 0;

                ApplyProductFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"POS initialization failed.\n{ex.Message}", "POS",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Left panel filtering ----------
        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyProductFilter();

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyProductFilter();

        private void ApplyProductFilter()
        {
            var selectedCat = (CategoryCombo.SelectedItem as string) ?? "All";
            var term = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();

            var query = Vm.MenuProducts.AsEnumerable();

            if (!string.Equals(selectedCat, "All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(m => string.Equals(m.Category ?? "", selectedCat, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(term))
                query = query.Where(m => (m.Name ?? "").ToLowerInvariant().Contains(term));

            _filteredMenu.Clear();
            _filteredMenu.AddRange(query);
            ProductList.Items.Refresh();
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not MenuModel product) return;

            var existing = Vm.EditingItems.FirstOrDefault(x => x.ProductId == product.Id);
            if (existing != null)
            {
                existing.Quantity += 1;
            }
            else
            {
                Vm.EditingItems.Add(new OrderItemModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price ?? 0m,
                    Quantity = 1
                });
            }
            Vm.RecalcEditingTotal();
        }

        // ---------- Right panel (same handlers as admin editor) ----------
        private void AddLine_Click(object sender, RoutedEventArgs e) => Vm.AddLine();

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is OrderItemModel item)
            {
                Vm.RemoveLine(item);
                Vm.RecalcEditingTotal();
            }
        }

        private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) => Vm.RecalcEditingTotal();

        // When product changes in the grid, set fields from menu
        private void ProductCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is OrderItemModel item)
            {
                if (cb.SelectedItem is MenuModel chosen)
                {
                    item.ProductId = chosen.Id;
                    item.UnitPrice = chosen.Price ?? 0m;
                    item.ProductName = chosen.Name;
                    Vm.RecalcEditingTotal();
                }
            }
        }

        private void SaveEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Vm.EditingOrder!.OrderType = Vm.SelectedOrderType;
                Vm.SaveEditing();   // persist order
                Vm.BeginAdd();      // ready for next order
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save order.\n{ex.Message}", "POS",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelEditor_Click(object sender, RoutedEventArgs e)
        {
            Vm.BeginAdd();          // clear current editing order
        }
    }
}