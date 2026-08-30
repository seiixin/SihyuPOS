#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SihyuPOSPayroll.Models;

namespace SihyuPOSPayroll.Views.Cashier.POS
{
    /// <summary>
    /// Two-column Settle Payment modal.
    /// Left  = payment fields (method, total, cash received, change, paid, remarks).
    /// Right = numpad for touch-screen entry.
    /// Raises <see cref="DialogClosed"/> with a <see cref="SettlePaymentResult"/> on
    /// Save/Print, or null on Cancel.
    /// </summary>
    public partial class SettlePaymentDialog : UserControl
    {
        // ── Output ───────────────────────────────────────────────────────────
        public event EventHandler<SettlePaymentResult?>? DialogClosed;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly decimal _total;
        private string _numpadBuffer = "0";

        // ── Constructor ───────────────────────────────────────────────────────
        private bool _initialized = false;

        public SettlePaymentDialog(decimal total, int orderId)
        {
            InitializeComponent();
            _initialized = true;

            _total = total;

            SubtitleText.Text    = $"Order #{orderId}";
            TotalText.Text       = $"\u20b1{total:N2}";
            PaidText.Text        = $"\u20b1{total:N2}";
            NumpadDisplay.Text   = "0";
            CashReceivedBox.Text = "0";

            RefreshChange(0m);

            // Auto-focus and select-all the cash received box as soon as the
            // dialog is rendered — must use Loaded + InvokeAsync(Input priority)
            // so the visual tree is fully ready before we move focus.
            Loaded += (_, __) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    CashReceivedBox.Focus();
                    CashReceivedBox.SelectAll();
                }, DispatcherPriority.Input);
            };

            // Keyboard shortcuts inside the dialog:
            //   Enter → Print (save + print receipt)
            //   F4    → Save  (save only, no print)
            //   Esc   → Cancel
            PreviewKeyDown += Dialog_PreviewKeyDown;
        }

        private void Dialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    // If focus is inside the Cash Received TextBox, Enter first
                    // commits the numpad buffer, then triggers Print on second press.
                    // Simpler: always trigger Print (cashier confirms with Enter).
                    if (SaveBtn.IsEnabled)
                    {
                        e.Handled = true;
                        Close(printReceipt: true);
                    }
                    break;

                case Key.F4:
                    if (SaveBtn.IsEnabled)
                    {
                        e.Handled = true;
                        Close(printReceipt: false);
                    }
                    break;

                case Key.Escape:
                    e.Handled = true;
                    DialogClosed?.Invoke(this, null);
                    break;
            }
        }

        // ── Payment method radio ──────────────────────────────────────────────
        private void PayMethod_Checked(object sender, RoutedEventArgs e) { }

        private string SelectedPaymentMethod()
        {
            if (RbGcash.IsChecked  == true) return "GCash";
            if (RbOnline.IsChecked == true) return "Online Banking";
            return "Cash";
        }

        // ── Cash Received TextBox ─────────────────────────────────────────────
        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = (TextBox)sender;
            string candidate = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength)
                                      .Insert(tb.SelectionStart, e.Text);
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(
                candidate, @"^\d{0,10}(\.\d{0,2})?$");
        }

        private void CashReceivedBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // SelectAll deferred so WPF doesn't immediately deselect after focus
            Dispatcher.InvokeAsync(() => CashReceivedBox.SelectAll(), DispatcherPriority.Input);
        }

        private void CashReceived_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_initialized) return;

            if (!decimal.TryParse(CashReceivedBox.Text,
                    NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cash))
                cash = 0m;

            // Keep numpad display in sync when user types directly
            _numpadBuffer      = string.IsNullOrWhiteSpace(CashReceivedBox.Text) ? "0" : CashReceivedBox.Text;
            NumpadDisplay.Text = _numpadBuffer;

            RefreshChange(cash);
        }

        // ── Change calculation + UI state ─────────────────────────────────────
        private void RefreshChange(decimal cashReceived)
        {
            if (!_initialized) return;

            decimal change    = cashReceived - _total;
            bool    sufficient = cashReceived >= _total;

            ChangeText.Text       = sufficient ? $"\u20b1{change:N2}" : "--";
            ChangeText.Foreground = sufficient
                ? new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81))
                : new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));

            InsufficientWarning.Visibility = sufficient ? Visibility.Collapsed : Visibility.Visible;
            SaveBtn.IsEnabled              = sufficient;
            PrintBtn.IsEnabled             = sufficient;
        }

        // ── Numpad ────────────────────────────────────────────────────────────
        private void Numpad_Click(object sender, RoutedEventArgs e)
        {
            string key = (sender as Button)?.Tag?.ToString() ?? string.Empty;

            switch (key)
            {
                case "back":
                    _numpadBuffer = _numpadBuffer.Length > 1
                        ? _numpadBuffer[..^1]
                        : "0";
                    break;

                case "enter":
                    CommitNumpad();
                    return;

                case ".":
                    if (!_numpadBuffer.Contains('.'))
                        _numpadBuffer += ".";
                    break;

                default: // digit
                    if (_numpadBuffer == "0")
                        _numpadBuffer = key;
                    else
                        _numpadBuffer += key;
                    break;
            }

            NumpadDisplay.Text = _numpadBuffer;
        }

        private void CommitNumpad()
        {
            // Writing to the TextBox fires CashReceived_TextChanged automatically
            CashReceivedBox.Text = _numpadBuffer;
            NumpadDisplay.Text   = _numpadBuffer;
        }

        // ── Quick-amount shortcuts ────────────────────────────────────────────
        private void Quick_Click(object sender, RoutedEventArgs e)
        {
            string tag = (sender as Button)?.Tag?.ToString() ?? "0";
            _numpadBuffer = tag;
            CommitNumpad();
        }

        // ── Save / Print / Cancel ─────────────────────────────────────────────
        private void Save_Click(object sender, RoutedEventArgs e)  => Close(printReceipt: false);
        private void Print_Click(object sender, RoutedEventArgs e) => Close(printReceipt: true);
        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogClosed?.Invoke(this, null);

        private void Close(bool printReceipt)
        {
            if (!decimal.TryParse(CashReceivedBox.Text,
                    NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cash))
                cash = _total;

            if (cash < _total)
            {
                InsufficientWarning.Visibility = Visibility.Visible;
                return;
            }

            var result = new SettlePaymentResult
            {
                PaymentMethod = SelectedPaymentMethod(),
                Total         = _total,
                CashReceived  = cash,
                Remarks       = RemarksBox.Text.Trim(),
                PrintReceipt  = printReceipt,
            };

            DialogClosed?.Invoke(this, result);
        }
    }
}
