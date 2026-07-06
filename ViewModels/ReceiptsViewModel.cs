using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SihyuPOSPayroll.ViewModels
{
    public class ReceiptsViewModel : INotifyPropertyChanged
    {
        // ----------- Bindable state -----------
        private string? _searchText;
        public string? SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                {
                    // live search (optional)
                    LoadReceipts(_searchText);
                }
            }
        }

        private ReceiptsModel? _selected;
        public ReceiptsModel? Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        public ObservableCollection<ReceiptsModel> Receipts { get; } = new();

        // ----------- Commands -----------
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand PrintCommand { get; }

        // ----------- Ctor -----------
        public ReceiptsViewModel()
        {
            RefreshCommand = new RelayCommand(_ => LoadReceipts());
            SearchCommand = new RelayCommand(_ => LoadReceipts(SearchText));
            ClearSearchCommand = new RelayCommand(_ => { SearchText = string.Empty; LoadReceipts(); });

            AddCommand = new RelayCommand(_ => AddReceipt(), _ => true);
            EditCommand = new RelayCommand(_ => EditReceipt(), _ => Selected != null);
            DeleteCommand = new RelayCommand(_ => DeleteReceipt(), _ => Selected != null);

            // IMPORTANT: PrintCommand now accepts parameter
            PrintCommand = new RelayCommand(p => PrintReceipt(p));

            LoadReceipts();
        }

        // ----------- Data ops -----------
        public void LoadReceipts(string? term = null)
        {
            try
            {
                // Non-fatal bulk sync so Paid orders have receipts
                try { ReceiptsServices.EnsureAllForPaidOrders(); } catch { /* ignore */ }

                var data = ReceiptsServices.GetAllReceipts(term);
                Receipts.Clear();
                foreach (var r in data) Receipts.Add(r);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load receipts.\n{ex.Message}", "Receipts",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddReceipt()
        {
            try
            {
                // Stub: replace with your editor dialog
                var newModel = new ReceiptsModel
                {
                    OrderId = 0,
                    Amount = 0m
                };

                var newId = ReceiptsServices.Create(newModel);
                var created = ReceiptsServices.GetById(newId);
                if (created != null) Receipts.Insert(0, created);
                Selected = created;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create receipt.\n{ex.Message}", "Receipts",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditReceipt()
        {
            if (Selected == null) return;

            try
            {
                ReceiptsServices.Update(Selected);

                var updated = ReceiptsServices.GetById(Selected.ReceiptId);
                if (updated != null)
                {
                    var idx = Receipts.ToList().FindIndex(r => r.ReceiptId == updated.ReceiptId);
                    if (idx >= 0) Receipts[idx] = updated;
                    Selected = updated;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update receipt.\n{ex.Message}", "Receipts",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteReceipt()
        {
            if (Selected == null) return;

            var confirm = MessageBox.Show(
                $"Delete receipt #{Selected.ReceiptId}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                ReceiptsServices.Delete(Selected.ReceiptId);
                var toRemove = Selected;
                Selected = null;
                var hit = Receipts.FirstOrDefault(r => r.ReceiptId == toRemove.ReceiptId);
                if (hit != null) Receipts.Remove(hit);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete receipt.\n{ex.Message}", "Receipts",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ----------- Print / Export flow -----------
        // Accepts CommandParameter:
        //  - int receiptId
        //  - ReceiptsModel row
        //  - or falls back to Selected
        private void PrintReceipt(object? parameter)
        {
            try
            {
                int receiptId = 0;

                if (parameter is int idParam) receiptId = idParam;
                else if (parameter is ReceiptsModel row) receiptId = row.ReceiptId;
                else if (Selected != null) receiptId = Selected.ReceiptId;

                if (receiptId <= 0)
                {
                    MessageBox.Show("Select a receipt to print.", "Print",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Always fetch fresh details (avoids stale data after sync)
                var details = ReceiptsServices.GetDetailsByReceiptId(receiptId);
                if (details == null)
                {
                    MessageBox.Show("Receipt details not found.", "Print",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Info popup (optional � keep if you want)
                var h = details.Header;
                var info =
                    $"Receipt ID : {h.ReceiptId}\n" +
                    $"Order ID   : {h.OrderId}\n" +
                    $"Table      : {h.TableNumber}\n" +
                    $"Date       : {h.Date}\n" +
                    $"Amount     : {h.Amount:0.##}\n" +
                    $"Lines      : {details.Lines.Count}\n\n" +
                    "Do you want to export this receipt?";
                MessageBox.Show(info, "Receipt Info", MessageBoxButton.OK, MessageBoxImage.Information);

                // Choose export target
                var choice = MessageBox.Show(
                    "Choose export format:\n\nYes = PDF (Print dialog)\nNo = JPG (save image)\nCancel = Do nothing",
                    "Export Receipt",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice == MessageBoxResult.Cancel) return;

                if (choice == MessageBoxResult.Yes)
                {
                    ExportReceiptAsPdf(details);
                }
                else if (choice == MessageBoxResult.No)
                {
                    ExportReceiptAsJpg(details);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print failed:\n{ex.Message}", "Print",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportReceiptAsPdf(ReceiptDetailsModel d)
        {
            var doc = BuildReceiptDocument(d);
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() == true)
            {
                doc.PageHeight = dlg.PrintableAreaHeight;
                doc.PageWidth = dlg.PrintableAreaWidth;
                dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,
                    $"Receipt #{d.Header.ReceiptId}");
            }
        }

        private void ExportReceiptAsJpg(ReceiptDetailsModel d)
        {
            var visual = BuildReceiptVisual(d);

            double width = 900, height = 700;
            visual.Measure(new Size(width, height));
            visual.Arrange(new Rect(0, 0, width, height));
            visual.UpdateLayout();

            var rtb = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            var sfd = new SaveFileDialog
            {
                Title = "Save Receipt as JPG",
                Filter = "JPEG Image (*.jpg)|*.jpg",
                FileName = $"receipt_{d.Header.ReceiptId}.jpg"
            };

            if (sfd.ShowDialog() == true)
            {
                using var fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write);
                encoder.Save(fs);
                MessageBox.Show("JPG saved successfully.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ----------- Builders (Visual + FlowDocument) -----------
        private FrameworkElement BuildReceiptVisual(ReceiptDetailsModel d)
        {
            var h = d.Header;

            var root = new Grid
            {
                Background = Brushes.White,
                Margin = new Thickness(24)
            };

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0 title
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1 meta
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2 items header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3 items
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4 total
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5 footer

            var title = new TextBlock
            {
                Text = "SihyuPOS� � Receipt",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            var meta = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 12) };
            meta.Children.Add(new TextBlock { Text = $"Receipt ID : {h.ReceiptId}", FontSize = 16 });
            meta.Children.Add(new TextBlock { Text = $"Order ID   : {h.OrderId}", FontSize = 16 });
            meta.Children.Add(new TextBlock { Text = $"Table      : {h.TableNumber}", FontSize = 16 });
            meta.Children.Add(new TextBlock { Text = $"Date       : {h.Date}", FontSize = 16 });
            Grid.SetRow(meta, 1);
            root.Children.Add(meta);

            var hdr = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); // Product
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Qty
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Unit
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Subtotal

            hdr.Children.Add(MakeCell("Item", true, 0));
            hdr.Children.Add(MakeCell("Qty", true, 1));
            hdr.Children.Add(MakeCell("Unit", true, 2));
            hdr.Children.Add(MakeCell("Subtotal", true, 3));

            Grid.SetRow(hdr, 2);
            root.Children.Add(hdr);

            var items = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            foreach (var line in d.Lines)
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                row.Children.Add(MakeCell(line.ProductName ?? $"#{line.ProductId}", false, 0));
                row.Children.Add(MakeCell(line.Quantity.ToString(), false, 1));
                row.Children.Add(MakeCell($"{line.UnitPrice:0.##}", false, 2));
                row.Children.Add(MakeCell($"{line.Subtotal:0.##}", false, 3));
                items.Children.Add(row);
            }
            Grid.SetRow(items, 3);
            root.Children.Add(items);

            var total = new TextBlock
            {
                Text = $"TOTAL: {d.GrandTotal:0.##}",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 0, 6)
            };
            Grid.SetRow(total, 4);
            root.Children.Add(total);

            var footer = new TextBlock
            {
                Text = "Thank you for dining with us!",
                FontSize = 14,
                Margin = new Thickness(0, 12, 0, 0),
                Opacity = 0.8
            };
            Grid.SetRow(footer, 5);
            root.Children.Add(footer);

            return root;

            static FrameworkElement MakeCell(string text, bool header, int col)
            {
                var tb = new TextBlock
                {
                    Text = text,
                    FontSize = header ? 15 : 14,
                    FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                    Margin = new Thickness(2, 2, 2, 2)
                };
                Grid.SetColumn(tb, col);
                return tb;
            }
        }

        private FlowDocument BuildReceiptDocument(ReceiptDetailsModel d)
        {
            var fd = new FlowDocument
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                PagePadding = new Thickness(30),
                ColumnWidth = double.PositiveInfinity,
                TextAlignment = TextAlignment.Left
            };

            var title = new Paragraph(new Bold(new Run("SihyuPOS� � Receipt")))
            { FontSize = 18, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
            fd.Blocks.Add(title);

            var h = d.Header;
            fd.Blocks.Add(new Paragraph(new Run($"Receipt #: {h.ReceiptId}")));
            fd.Blocks.Add(new Paragraph(new Run($"Order   #: {h.OrderId}")));
            fd.Blocks.Add(new Paragraph(new Run($"Table     : {h.TableNumber}")));
            fd.Blocks.Add(new Paragraph(new Run($"Date      : {h.Date}")));
            fd.Blocks.Add(new Paragraph(new Run(" ")));

            var table = new Table { CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(260) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(120) });
            table.RowGroups.Add(new TableRowGroup());

            var headerRow = new TableRow();
            headerRow.Cells.Add(HeaderCell("Product"));
            headerRow.Cells.Add(HeaderCell("Qty"));
            headerRow.Cells.Add(HeaderCell("Unit"));
            headerRow.Cells.Add(HeaderCell("Subtotal"));
            table.RowGroups[0].Rows.Add(headerRow);

            foreach (var line in d.Lines)
            {
                var row = new TableRow();
                row.Cells.Add(Cell(line.ProductName ?? "(item)"));
                row.Cells.Add(Cell(line.Quantity.ToString()));
                row.Cells.Add(Cell(line.UnitPrice.ToString("0.##")));
                row.Cells.Add(Cell((line.UnitPrice * line.Quantity).ToString("0.##")));
                table.RowGroups[0].Rows.Add(row);
            }

            fd.Blocks.Add(table);

            var totalPara = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            totalPara.Inlines.Add(new Bold(new Run($"Total: {d.GrandTotal:0.##}")));
            fd.Blocks.Add(totalPara);

            var paidPara = new Paragraph { TextAlignment = TextAlignment.Right };
            paidPara.Inlines.Add(new Run($"Amount Paid: {h.Amount:0.##}"));
            fd.Blocks.Add(paidPara);

            fd.Blocks.Add(new Paragraph(new Run("Thank you!")));
            return fd;

            // helpers
            static TableCell HeaderCell(string text) =>
                new TableCell(new Paragraph(new Bold(new Run(text))))
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(0, 2, 0, 4)
                };

            static TableCell Cell(string text) =>
                new TableCell(new Paragraph(new Run(text)))
                {
                    Padding = new Thickness(0, 2, 0, 2)
                };
        }

        // ----------- INotifyPropertyChanged -----------
        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
