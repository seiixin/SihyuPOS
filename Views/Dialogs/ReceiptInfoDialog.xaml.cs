using SihyuPOSPayroll.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SihyuPOSPayroll.Views.Dialogs
{
    /// <summary>
    /// Themed receipt info dialog.
    /// After ShowDialog(), check <see cref="ExportChoice"/>:
    ///   PDF = ExportPdf, JPG = ExportJpg, null = closed without export.
    /// </summary>
    public partial class ReceiptInfoDialog : Window
    {
        public enum ReceiptExportChoice { ExportPdf, ExportJpg }

        /// <summary>Set by the action buttons before Close().</summary>
        public ReceiptExportChoice? ExportChoice { get; private set; }

        public ReceiptInfoDialog(ReceiptDetailsModel details)
        {
            InitializeComponent();

            var h = details.Header;

            TitleText.Text       = $"Receipt #{h.ReceiptId}";
            ReceiptIdText.Text   = $"#{h.ReceiptId}";
            OrderIdText.Text     = $"#{h.OrderId}";
            DateText.Text        = string.IsNullOrWhiteSpace(h.Date) ? "—" : h.Date;
            TotalText.Text       = $"₱{details.GrandTotal:N2}";

            // Build display-friendly line items
            var rows = details.Lines.Select(l => new LineRow(l)).ToList();
            ItemsList.ItemsSource = rows;
        }

        // Drag to move (no title bar chrome)
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter)   { PdfButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
            if (e.Key == Key.F4)      { JpgButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
            if (e.Key == Key.Escape)  { CloseButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
        }

        private void PdfButton_Click(object sender, RoutedEventArgs e)
        {
            ExportChoice = ReceiptExportChoice.ExportPdf;
            DialogResult = true;
        }

        private void JpgButton_Click(object sender, RoutedEventArgs e)
        {
            ExportChoice = ReceiptExportChoice.ExportJpg;
            DialogResult = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ExportChoice = null;
            DialogResult = false;
        }

        // ── Thin presentation wrapper for the ItemsControl DataTemplate ──
        private sealed class LineRow
        {
            public string ProductName     { get; }
            public string UnitPriceFormatted { get; }
            public int    Quantity        { get; }
            public string SubtotalFormatted  { get; }

            public LineRow(ReceiptLineModel m)
            {
                ProductName          = m.ProductName ?? $"Product #{m.ProductId}";
                UnitPriceFormatted   = $"₱{m.UnitPrice:N2} each";
                Quantity             = m.Quantity;
                SubtotalFormatted    = $"₱{m.Subtotal:N2}";
            }
        }
    }
}
