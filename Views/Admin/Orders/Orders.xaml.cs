using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Admin.Orders
{
    public partial class Orders : UserControl
    {
        private OrdersViewModel Vm => (OrdersViewModel)DataContext;

        public Orders()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"XAML load failed.\n\n{ex}", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                DataContext = new OrdersViewModel();   // VM owns CRUD via OrderService
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Orders ViewModel.\n\n{ex}",
                    "Orders", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Double-click to edit
            OrdersGrid.MouseDoubleClick += (s, e) =>
            {
                if (OrdersGrid.SelectedItem is OrderModel row) EditSelected(row);
            };
        }

        // ---------- Search ----------
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var term = SearchBox.Text?.Trim() ?? string.Empty;

            Vm.LoadOrders();

            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.ToLowerInvariant();
                var filtered = Vm.Orders
                    .Where(o =>
                        (!string.IsNullOrEmpty(o.TableNumber) && o.TableNumber.ToLower().Contains(term)) ||
                        o.PaymentStatus.ToString().ToLower().Contains(term) ||
                        o.OrderStatus.ToString().ToLower().Contains(term))
                    .ToList();

                Vm.Orders.Clear();
                foreach (var o in filtered) Vm.Orders.Add(o);
            }
        }

        // ---------- Info (read-only) ----------
        private void InfoOrder_Click(object sender, RoutedEventArgs e)
        {
            if (OrdersGrid.SelectedItem is not OrderModel selected)
            {
                MessageBox.Show("Please select an order to view.", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Vm.BeginInfo(selected);           // fills InfoOrder + InfoItems
            InfoPanel.Visibility = Visibility.Visible;
        }

        private void CloseInfo_Click(object sender, RoutedEventArgs e)
        {
            InfoPanel.Visibility = Visibility.Collapsed;
        }

        // ---------- List actions ----------
        private void AddOrder_Click(object sender, RoutedEventArgs e)
        {
            Vm.BeginAdd();
            EditorPanel.Visibility = Visibility.Visible;
        }

        private void EditOrder_Click(object sender, RoutedEventArgs e)
        {
            if (OrdersGrid.SelectedItem is not OrderModel selected)
            {
                MessageBox.Show("Please select an order to edit.", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            EditSelected(selected);
        }

        private void EditSelected(OrderModel selected)
        {
            Vm.BeginEdit(selected);
            EditorPanel.Visibility = Visibility.Visible;
        }

        private void DeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (OrdersGrid.SelectedItem is not OrderModel row)
            {
                MessageBox.Show("Please select an order to delete.", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete order #{row.Id}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                Vm.DeleteOrder(row.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete order.\n{ex.Message}", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Editor overlay ----------
        private void AddLine_Click(object sender, RoutedEventArgs e) => Vm.AddLine();

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is OrderItemModel item) Vm.RemoveLine(item);
        }

        private void AnyEditorFieldChanged(object sender, TextChangedEventArgs e) => Vm.RecalcEditingTotal();

        private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) => Vm.RecalcEditingTotal();

        // When product changes, set UnitPrice/ProductName from menu
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
                Vm.SaveEditing();
                EditorPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save order.\n{ex.Message}", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelEditor_Click(object sender, RoutedEventArgs e)
        {
            EditorPanel.Visibility = Visibility.Collapsed;
        }
    }
}
