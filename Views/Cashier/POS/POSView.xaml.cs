using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
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
                DataContext = new OrdersViewModel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize POS ViewModel.\n\n{ex}",
                    "POS", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Loaded += POSView_Loaded;

            // Intercept every keystroke on this UserControl —
            // barcode scanners type digits fast without focus on any field.
            PreviewKeyDown += POS_PreviewKeyDown;
        }

        private void POSView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Vm.BeginAdd();

                ProductList.ItemsSource = _filteredMenu;

                var cats = Vm.MenuProducts
                             .Select(m => m.Category ?? "Uncategorized")
                             .Distinct()
                             .OrderBy(c => c)
                             .ToList();
                cats.Insert(0, "All");
                CategoryCombo.ItemsSource = cats;
                if (CategoryCombo.Items.Count > 0) CategoryCombo.SelectedIndex = 0;

                ApplyProductFilter();

                // Always start with focus on the barcode box
                BarcodeBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"POS initialization failed.\n{ex.Message}", "POS",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Barcode scanner key redirect ─────────────────────────────────────
        // Any digit/minus keypress that lands outside the BarcodeBox is silently
        // rerouted into it, so the scanner never needs the user to click first.
        private void POS_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Don't intercept keys while the new-item dialog is open
            if (DialogOverlay.Visibility == Visibility.Visible) return;

            // Don't redirect when the user is typing in the search box or
            // editing a cell in the cart grid.
            bool isInOtherInput = Keyboard.FocusedElement is TextBox tb
                                  && tb != BarcodeBox;

            if (IsBarcodeKey(e.Key) && !isInOtherInput)
            {
                if (Keyboard.FocusedElement != BarcodeBox)
                {
                    char? ch = KeyToChar(e.Key);
                    if (ch.HasValue)
                    {
                        int caret = BarcodeBox.SelectionStart;
                        string text = (BarcodeBox.Text ?? string.Empty)
                                        .Remove(caret, BarcodeBox.SelectionLength)
                                        .Insert(caret, ch.Value.ToString());
                        BarcodeBox.Text           = text;
                        BarcodeBox.SelectionStart  = caret + 1;
                        BarcodeBox.SelectionLength = 0;
                        e.Handled = true;
                    }
                    BarcodeBox.Focus();
                }
            }

            if (e.Key == Key.Enter && !isInOtherInput && !e.Handled)
            {
                string bc = BarcodeBox.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(bc))
                {
                    e.Handled = true;
                    CommitBarcode();
                }
            }
        }

        // ── Barcode box events ────────────────────────────────────────────────
        private void BarcodeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string bc = BarcodeBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(bc))
            {
                BarcodeFeedback.Text       = string.Empty;
                BarcodeSpinner.Visibility  = Visibility.Collapsed;
                return;
            }

            // Live preview — show matching product name if barcode is already known
            var match = FindByBarcode(bc);
            if (match != null)
            {
                BarcodeFeedback.Text       = match.Name ?? string.Empty;
                BarcodeFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
            }
            else
            {
                BarcodeFeedback.Text = string.Empty;
            }
        }

        private void BarcodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                CommitBarcode();
            }
        }

        private void BarcodeAdd_Click(object sender, RoutedEventArgs e)
            => CommitBarcode();

        // ── Core barcode commit ───────────────────────────────────────────────
        private void CommitBarcode()
        {
            string bc = BarcodeBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(bc)) { BarcodeBox.Focus(); return; }

            var product = FindByBarcode(bc);
            if (product != null)
            {
                AddToCart(product);
                BarcodeFeedback.Text       = $"Added: {product.Name}";
                BarcodeFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
                BarcodeBox.Clear();
                BarcodeBox.Focus();
            }
            else
            {
                // Barcode not in inventory — show the add-to-inventory modal
                BarcodeBox.Clear();
                ShowNewItemDialog(bc);
            }
        }

        // ── New-item dialog (shown when barcode not found) ────────────────────
        private void ShowNewItemDialog(string barcode)
        {
            var dialog = new POSNewItemDialog(barcode);
            dialog.DialogClosed += OnNewItemDialogClosed;

            DialogHost.Content     = dialog;
            DialogOverlay.Visibility = Visibility.Visible;
        }

        private void OnNewItemDialogClosed(object? sender, InventoryItem? savedItem)
        {
            // Always tear down the overlay first
            DialogOverlay.Visibility = Visibility.Collapsed;
            DialogHost.Content       = null;

            if (savedItem == null)
            {
                // User cancelled — just restore focus
                BarcodeFeedback.Text       = string.Empty;
                BarcodeBox.Focus();
                return;
            }

            // Item was saved to inventory — reload the product list
            Vm.LoadMenu();

            // Rebuild the category combo from the refreshed list
            var cats = Vm.MenuProducts
                         .Select(m => m.Category ?? "Uncategorized")
                         .Distinct()
                         .OrderBy(c => c)
                         .ToList();
            cats.Insert(0, "All");
            CategoryCombo.ItemsSource = cats;

            ApplyProductFilter();

            // Auto-add the newly created item to cart by barcode
            var product = FindByBarcode(savedItem.Barcode ?? string.Empty);
            if (product != null)
            {
                AddToCart(product);
                BarcodeFeedback.Text       = $"Added: {product.Name}";
                BarcodeFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
            }
            else
            {
                BarcodeFeedback.Text       = $"Saved to inventory: {savedItem.ProductName}";
                BarcodeFeedback.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
            }

            BarcodeBox.Focus();
        }

        /// <summary>
        /// Finds a MenuModel from the loaded product list by exact barcode match.
        /// </summary>
        private MenuModel? FindByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            return Vm.MenuProducts.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Barcode) &&
                p.Barcode.Equals(barcode.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // ── Key helpers (shared with inventory scanner logic) ─────────────────
        private static bool IsBarcodeKey(Key k) =>
            k is Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4
              or Key.D5 or Key.D6 or Key.D7 or Key.D8 or Key.D9
              or Key.NumPad0 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3 or Key.NumPad4
              or Key.NumPad5 or Key.NumPad6 or Key.NumPad7 or Key.NumPad8 or Key.NumPad9
              or Key.OemMinus or Key.Subtract;

        private static char? KeyToChar(Key k)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            return k switch
            {
                Key.D0 => shift ? ')' : '0', Key.D1 => shift ? '!' : '1',
                Key.D2 => shift ? '@' : '2', Key.D3 => shift ? '#' : '3',
                Key.D4 => shift ? '$' : '4', Key.D5 => shift ? '%' : '5',
                Key.D6 => shift ? '^' : '6', Key.D7 => shift ? '&' : '7',
                Key.D8 => shift ? '*' : '8', Key.D9 => shift ? '(' : '9',
                Key.NumPad0 => '0', Key.NumPad1 => '1', Key.NumPad2 => '2',
                Key.NumPad3 => '3', Key.NumPad4 => '4', Key.NumPad5 => '5',
                Key.NumPad6 => '6', Key.NumPad7 => '7', Key.NumPad8 => '8',
                Key.NumPad9 => '9',
                Key.OemMinus => shift ? '_' : '-',
                Key.Subtract => '-',
                _ => null
            };
        }

        // ── Left panel filtering ──────────────────────────────────────────────
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

        // ── Product image loader ──────────────────────────────────────────────
        private void ProductImage_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Image img) return;
            var path = img.Tag as string;
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                var uri = Uri.IsWellFormedUriString(path, UriKind.Absolute)
                          ? new Uri(path, UriKind.Absolute)
                          : new Uri(System.IO.Path.GetFullPath(path), UriKind.Absolute);

                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource        = uri;
                bmp.CacheOption      = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 148;
                bmp.EndInit();
                bmp.Freeze();

                img.Source     = bmp;
                img.Visibility = Visibility.Visible;
            }
            catch
            {
                img.Visibility = Visibility.Collapsed;
            }
        }

        // ── Add product to cart (card click) ──────────────────────────────────
        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            var product = (sender as FrameworkElement)?.Tag as MenuModel;
            if (product == null) return;
            AddToCart(product);

            // Return focus to barcode box so scanner is always ready
            BarcodeBox.Focus();
        }

        /// <summary>
        /// Core cart-add logic: increment if already in cart, otherwise add new row.
        /// Recalculates total and scrolls the cart to the affected row.
        /// </summary>
        private void AddToCart(MenuModel product)
        {
            var existing = Vm.EditingItems.FirstOrDefault(x => x.ProductId == product.Id);
            if (existing != null)
            {
                existing.Quantity += 1;
                Vm.RecalcEditingTotal();
                ScrollCartTo(existing);
            }
            else
            {
                var newItem = new OrderItemModel
                {
                    ProductId   = product.Id,
                    ProductName = product.Name,
                    UnitPrice   = product.Price ?? 0m,
                    Quantity    = 1
                };
                Vm.EditingItems.Add(newItem);
                Vm.RecalcEditingTotal();
                ScrollCartTo(newItem);
            }
        }

        private void ScrollCartTo(OrderItemModel item)
        {
            Dispatcher.InvokeAsync(() => ItemsGrid.ScrollIntoView(item),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // ── Right panel cart handlers ─────────────────────────────────────────
        private void AddLine_Click(object sender, RoutedEventArgs e) => Vm.AddLine();

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is OrderItemModel item)
            {
                Vm.RemoveLine(item);
                Vm.RecalcEditingTotal();
            }
            BarcodeBox.Focus();
        }

        private void DecrQty_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not OrderItemModel item) return;
            if (item.Quantity <= 1)
            {
                Vm.RemoveLine(item);
            }
            else
            {
                item.Quantity -= 1;
            }
            Vm.RecalcEditingTotal();
            BarcodeBox.Focus();
        }

        private void IncrQty_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not OrderItemModel item) return;
            item.Quantity += 1;
            Vm.RecalcEditingTotal();
            BarcodeBox.Focus();
        }

        private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
            => Vm.RecalcEditingTotal();

        private void ProductCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is OrderItemModel item)
            {
                if (cb.SelectedItem is MenuModel chosen)
                {
                    item.ProductId   = chosen.Id;
                    item.UnitPrice   = chosen.Price ?? 0m;
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

                // POS always completes as Paid — the dropdown is hidden in this view
                Vm.EditingPaymentStatus = PaymentStatus.Paid;

                Vm.SaveEditing();
                Vm.BeginAdd();
                BarcodeFeedback.Text = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save order.\n{ex.Message}", "POS",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BarcodeBox.Focus();
            }
        }

        private void CancelEditor_Click(object sender, RoutedEventArgs e)
        {
            Vm.BeginAdd();
            BarcodeFeedback.Text = string.Empty;
            BarcodeBox.Focus();
        }
    }
}
