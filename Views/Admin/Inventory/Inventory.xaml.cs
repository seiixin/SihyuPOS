using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Admin.Inventory
{
    public partial class Inventory : UserControl
    {
        public Inventory()
        {
            InitializeComponent();
            Loaded             += Inventory_Loaded;
            DataContextChanged += Inventory_DataContextChanged;
            PreviewKeyDown     += Inventory_PreviewKeyDown;
        }

        private InventoryViewModel? VM => DataContext as InventoryViewModel;

        // ── DataContext / ViewModel wiring ────────────────────────────────────
        private InventoryViewModel? _subscribedVm;

        private void Inventory_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.LookupStatusChanged -= OnLookupStatusChanged;
                _subscribedVm.PropertyChanged     -= OnVmPropertyChanged;
            }
            _subscribedVm = e.NewValue as InventoryViewModel;
            if (_subscribedVm != null)
            {
                _subscribedVm.LookupStatusChanged += OnLookupStatusChanged;
                _subscribedVm.PropertyChanged     += OnVmPropertyChanged;
            }
        }

        private void Inventory_Loaded(object sender, RoutedEventArgs e)
        {
            if (_subscribedVm == null && VM != null)
            {
                _subscribedVm = VM;
                _subscribedVm.LookupStatusChanged += OnLookupStatusChanged;
                _subscribedVm.PropertyChanged     += OnVmPropertyChanged;
            }
            ApplyColumnVisibility();
            QuickAddBarcodeBox.Focus();
        }

        // ── Column toggle ─────────────────────────────────────────────────────
        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(InventoryViewModel.ShowQty)
                               or nameof(InventoryViewModel.ShowExpiry)
                               or nameof(InventoryViewModel.ShowCategory))
                ApplyColumnVisibility();
        }

        private void ApplyColumnVisibility()
        {
            var vm = VM;
            if (vm == null) return;
            QtyColumn.Visibility      = vm.ShowQty      ? Visibility.Visible : Visibility.Collapsed;
            ExpiryColumn.Visibility   = vm.ShowExpiry   ? Visibility.Visible : Visibility.Collapsed;
            CategoryColumn.Visibility = vm.ShowCategory ? Visibility.Visible : Visibility.Collapsed;
        }

        // Toggles called from inside the popup — just re-apply after binding updates
        private void ColumnToggle_Click(object sender, RoutedEventArgs e)
            => ApplyColumnVisibility();

        // Opens / closes the settings popup
        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
            => SettingsPopup.IsOpen = !SettingsPopup.IsOpen;

        // ── Lookup status callback ────────────────────────────────────────────
        private void OnLookupStatusChanged(string hint, bool isError)
        {
            LookupHintText.Text       = hint;
            LookupHintText.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))
                : new SolidColorBrush(Color.FromRgb(0x47, 0x7C, 0x60));

            SpinnerBorder.Visibility = (VM?.IsLookingUp ?? false)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ── Page-level key capture (barcode scanner redirect) ─────────────────
        private void Inventory_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isInOtherEditable =
                Keyboard.FocusedElement is TextBox tb && tb != QuickAddBarcodeBox;

            if (IsBarcodeKey(e.Key) && !isInOtherEditable)
            {
                if (Keyboard.FocusedElement != QuickAddBarcodeBox)
                {
                    char? ch = KeyToChar(e.Key);
                    if (ch.HasValue)
                    {
                        int caret = QuickAddBarcodeBox.SelectionStart;
                        string text = (QuickAddBarcodeBox.Text ?? string.Empty)
                                        .Remove(caret, QuickAddBarcodeBox.SelectionLength)
                                        .Insert(caret, ch.Value.ToString());
                        QuickAddBarcodeBox.Text            = text;
                        QuickAddBarcodeBox.SelectionStart  = caret + 1;
                        QuickAddBarcodeBox.SelectionLength = 0;
                        e.Handled = true;
                    }
                    QuickAddBarcodeBox.Focus();
                }
            }

            if (e.Key == Key.Enter && !isInOtherEditable && !e.Handled)
            {
                string bc = QuickAddBarcodeBox.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(bc))
                {
                    e.Handled = true;
                    _ = DoQuickAddAsync();
                }
            }
        }

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

        private static bool IsBarcodeKey(Key k) =>
            k is Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4
              or Key.D5 or Key.D6 or Key.D7 or Key.D8 or Key.D9
              or Key.NumPad0 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3 or Key.NumPad4
              or Key.NumPad5 or Key.NumPad6 or Key.NumPad7 or Key.NumPad8 or Key.NumPad9
              or Key.OemMinus or Key.Subtract;

        // ── Quick Add box events ──────────────────────────────────────────────
        private void QuickAddBarcodeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string bc = QuickAddBarcodeBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(bc))
            {
                LookupHintText.Text      = string.Empty;
                SpinnerBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var hit = BarcodeLookupService.Lookup(bc);
            if (hit != null)
            {
                LookupHintText.Text       = "Found: " + hit.ProductName;
                LookupHintText.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
            }
            else
            {
                LookupHintText.Text = string.Empty;
            }
        }

        private void QuickAddBarcodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _ = DoQuickAddAsync();
            }
        }

        private void QuickAddBarcode_Click(object sender, RoutedEventArgs e)
            => _ = DoQuickAddAsync();

        // ── Core async quick-add ──────────────────────────────────────────────
        private async System.Threading.Tasks.Task DoQuickAddAsync()
        {
            string bc = QuickAddBarcodeBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(bc)) { QuickAddBarcodeBox.Focus(); return; }

            var vm = VM;
            if (vm == null) return;

            QuickAddBarcodeBox.IsEnabled = false;
            SpinnerBorder.Visibility     = Visibility.Visible;

            try
            {
                await vm.QuickAddByBarcodeAsync(bc);
            }
            finally
            {
                QuickAddBarcodeBox.IsEnabled = true;
                if (!vm.IsLookingUp)
                    SpinnerBorder.Visibility = Visibility.Collapsed;
                QuickAddBarcodeBox.Clear();
                QuickAddBarcodeBox.Focus();
            }
        }
    }
}
