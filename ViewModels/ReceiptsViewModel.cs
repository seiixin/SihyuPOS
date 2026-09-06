﻿﻿﻿using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.Views.Dialogs;
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
        public ICommand RenameStoreCommand { get; }

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

            RenameStoreCommand = new RelayCommand(_ => RenameStore());

            LoadReceipts();
        }

        // ----------- Store rename -----------
        private void RenameStore()
        {
            var dialog = new Views.Dialogs.RenameStoreDialog(SettingsService.Instance.StoreName);
            var main = System.Windows.Application.Current.MainWindow;
            if (main != null && main.IsLoaded && main.IsVisible)
                dialog.Owner = main;

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
            {
                try
                {
                    SettingsService.Instance.SaveStoreName(dialog.NewName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save store name.\n{ex.Message}", "Rename Store",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ----------- Data ops -----------
        public void LoadReceipts(string? term = null)        {
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

                // Show themed receipt info dialog
                var dialog = new ReceiptInfoDialog(details);
                // Set Owner safely — only if MainWindow is already visible
                var main = System.Windows.Application.Current.MainWindow;
                if (main != null && main.IsLoaded && main.IsVisible)
                    dialog.Owner = main;

                dialog.ShowDialog();

                if (dialog.ExportChoice == ReceiptInfoDialog.ReceiptExportChoice.ExportPdf)
                {
                    ExportReceiptAsPdf(details);
                }
                else if (dialog.ExportChoice == ReceiptInfoDialog.ReceiptExportChoice.ExportJpg)
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
            // Measure true content height using the visual builder (StackPanel layout is accurate).
            // Then build the FlowDocument with PageHeight set to that exact height so no blank
            // space is added at the bottom.
            var visual = (FrameworkElement)BuildReceiptVisual(d);
            visual.Measure(new Size(ReceiptWidth, double.PositiveInfinity));
            double contentHeight = Math.Ceiling(visual.DesiredSize.Height) + 8; // +8 for bottom safety

            var doc = BuildReceiptDocument(d, contentHeight);
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() == true)
            {
                dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,
                    $"Receipt #{d.Header.ReceiptId}");
            }
        }

        private void ExportReceiptAsJpg(ReceiptDetailsModel d)
        {
            var visual = (FrameworkElement)BuildReceiptVisual(d);

            // Measure to actual content height
            double width = ReceiptWidth;
            visual.Measure(new Size(width, double.PositiveInfinity));
            double height = visual.DesiredSize.Height + 4;
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
        // Slim thermal receipt — 58 mm equivalent, monospaced layout

        private const double ReceiptWidth   = 310;
        private const double ReceiptPadding = 16;

        // Store name is read live from SettingsService so it reflects any rename.
        private static string StoreName => SettingsService.Instance.StoreName;

        /// <summary>Builds a slim thermal-style WPF visual for JPG export.</summary>
        private FrameworkElement BuildReceiptVisual(ReceiptDetailsModel d)
        {
            var h    = d.Header;
            var font = new FontFamily("Courier New");

            var root = new StackPanel
            {
                Background = Brushes.White,
                Width      = ReceiptWidth
            };

            void AddText(string text, double size = 10.5,
                         FontWeight? weight = null,
                         TextAlignment align = TextAlignment.Left,
                         double topMargin = 0,
                         Brush? color = null)
            {
                root.Children.Add(new TextBlock
                {
                    Text          = text,
                    FontFamily    = font,
                    FontSize      = size,
                    FontWeight    = weight ?? FontWeights.Normal,
                    TextAlignment = align,
                    Foreground    = color ?? Brushes.Black,
                    Margin        = new Thickness(ReceiptPadding, topMargin, ReceiptPadding, 0),
                    TextWrapping  = TextWrapping.Wrap
                });
            }

            void AddRule(double topMargin = 4) =>
                root.Children.Add(new Border
                {
                    Height     = 1,
                    Background = Brushes.Black,
                    Margin     = new Thickness(ReceiptPadding, topMargin, ReceiptPadding, 0)
                });

            // ── Header ──
            AddText(StoreName, 16, FontWeights.Bold, TextAlignment.Center, 14);
            AddText("Official Receipt", 10, null, TextAlignment.Center, 2, Brushes.Gray);
            AddRule(8);

            // ── Meta ──
            var metaGrid = new Grid { Margin = new Thickness(ReceiptPadding, 6, ReceiptPadding, 0) };
            metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void MetaRow(string label, string value, int rowIdx)
            {
                metaGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var lbl = new TextBlock { Text = label, FontFamily = font, FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 1, 8, 1) };
                var val = new TextBlock { Text = value, FontFamily = font, FontSize = 10, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 1, 0, 1) };
                Grid.SetRow(lbl, rowIdx); Grid.SetColumn(lbl, 0); metaGrid.Children.Add(lbl);
                Grid.SetRow(val, rowIdx); Grid.SetColumn(val, 1); metaGrid.Children.Add(val);
            }
            MetaRow("Receipt #:", $"{h.ReceiptId}", 0);
            MetaRow("Order #:",   $"{h.OrderId}",   1);
            MetaRow("Date:",       string.IsNullOrWhiteSpace(h.Date) ? "—" : h.Date, 2);
            root.Children.Add(metaGrid);
            AddRule(6);

            // ── Column headers ──
            var hdrGrid = new Grid { Margin = new Thickness(ReceiptPadding, 5, ReceiptPadding, 0) };
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });

            TextBlock HdrCell(string t, int col, TextAlignment a = TextAlignment.Left)
            {
                var tb = new TextBlock { Text = t, FontFamily = font, FontSize = 9.5, FontWeight = FontWeights.Bold, TextAlignment = a, Foreground = Brushes.DimGray };
                Grid.SetColumn(tb, col); return tb;
            }
            hdrGrid.Children.Add(HdrCell("ITEM",   0));
            hdrGrid.Children.Add(HdrCell("QTY",    1, TextAlignment.Center));
            hdrGrid.Children.Add(HdrCell("AMOUNT", 2, TextAlignment.Right));
            root.Children.Add(hdrGrid);
            AddRule(3);

            // ── Line items ──
            foreach (var line in d.Lines)
            {
                var name = !string.IsNullOrWhiteSpace(line.ProductName)
                    ? line.ProductName
                    : $"Item #{line.ProductId}";

                var rowGrid = new Grid { Margin = new Thickness(ReceiptPadding, 5, ReceiptPadding, 0) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });

                var namePanel = new StackPanel();
                namePanel.Children.Add(new TextBlock
                {
                    Text = name, FontFamily = font, FontSize = 10.5,
                    TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Black
                });
                namePanel.Children.Add(new TextBlock
                {
                    Text = $"  @ {line.UnitPrice:N2}", FontFamily = font,
                    FontSize = 9, Foreground = Brushes.Gray
                });

                var qtyTb = new TextBlock
                {
                    Text = $"{line.Quantity}", FontFamily = font, FontSize = 10.5,
                    TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Top
                };
                var subTb = new TextBlock
                {
                    Text = $"{line.Subtotal:N2}", FontFamily = font, FontSize = 10.5,
                    TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Top
                };

                Grid.SetColumn(namePanel, 0);
                Grid.SetColumn(qtyTb, 1);
                Grid.SetColumn(subTb, 2);
                rowGrid.Children.Add(namePanel);
                rowGrid.Children.Add(qtyTb);
                rowGrid.Children.Add(subTb);
                root.Children.Add(rowGrid);
            }
            AddRule(6);

            // ── Totals ──
            var totGrid = new Grid { Margin = new Thickness(ReceiptPadding, 5, ReceiptPadding, 0) };
            totGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            totGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            totGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            totGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            void TotRow(string lbl, string val, int rowIdx, bool bold)
            {
                var fw = bold ? FontWeights.Bold : FontWeights.Normal;
                var fs = bold ? 12.0 : 10.5;
                var l = new TextBlock { Text = lbl, FontFamily = font, FontSize = fs, FontWeight = fw, Margin = new Thickness(0, 1, 8, 1) };
                var v = new TextBlock { Text = val, FontFamily = font, FontSize = fs, FontWeight = fw, TextAlignment = TextAlignment.Right, MinWidth = 64 };
                Grid.SetRow(l, rowIdx); Grid.SetColumn(l, 0); totGrid.Children.Add(l);
                Grid.SetRow(v, rowIdx); Grid.SetColumn(v, 1); totGrid.Children.Add(v);
            }
            TotRow("TOTAL",       $"{d.GrandTotal:N2}", 0, true);
            TotRow("Amount Paid", $"{h.Amount:N2}",     1, false);
            root.Children.Add(totGrid);
            AddRule(6);

            // ── Footer ──
            AddText("Thank you for your purchase!", 9.5, null, TextAlignment.Center, 8, Brushes.Gray);
            AddText("Please come again.", 9.5, null, TextAlignment.Center, 3, Brushes.Gray);
            AddText("", 8, null, TextAlignment.Center, 12); // bottom padding

            return root;
        }

        /// <summary>Builds a slim thermal-style FlowDocument for PDF/print output.</summary>
        private FlowDocument BuildReceiptDocument(ReceiptDetailsModel d, double pageHeight = 4000)
        {
            var h    = d.Header;
            var font = new FontFamily("Courier New");

            var fd = new FlowDocument
            {
                FontFamily    = font,
                FontSize      = 10,
                PageWidth     = 260,
                PageHeight    = pageHeight,    // caller passes measured content height
                PagePadding   = new Thickness(12, 28, 12, 28),
                ColumnWidth   = double.PositiveInfinity,
                TextAlignment = TextAlignment.Left
            };

            Paragraph Para(string text, double size = 10,
                            TextAlignment align = TextAlignment.Left,
                            bool bold = false, double spaceAfter = 0,
                            Brush? fg = null)
            {
                Inline inline = bold ? (Inline)new Bold(new Run(text)) : new Run(text);
                if (fg != null) inline.Foreground = fg;
                return new Paragraph(inline)
                {
                    FontSize      = size,
                    TextAlignment = align,
                    Margin        = new Thickness(0, 0, 0, spaceAfter),
                    LineHeight    = size * 1.35
                };
            }

            string Dashes() => new string('-', 30);

            // ── Header ──
            fd.Blocks.Add(Para(StoreName, 14, TextAlignment.Center, bold: true));
            fd.Blocks.Add(Para("Official Receipt", 9, TextAlignment.Center, fg: Brushes.Gray, spaceAfter: 3));
            fd.Blocks.Add(Para(Dashes(), 8, TextAlignment.Center, fg: Brushes.Gray, spaceAfter: 4));

            // ── Meta ──
            fd.Blocks.Add(Para($"Receipt # : {h.ReceiptId}", 9.5));
            fd.Blocks.Add(Para($"Order #   : {h.OrderId}",   9.5));
            fd.Blocks.Add(Para($"Date      : {(string.IsNullOrWhiteSpace(h.Date) ? "—" : h.Date)}", 9.5, spaceAfter: 3));
            fd.Blocks.Add(Para(Dashes(), 8, TextAlignment.Center, fg: Brushes.Gray, spaceAfter: 4));

            // ── Items table ──
            // Items table — columns sum to 236px (= PageWidth 260 − padding 12×2)
            var tbl = new Table { CellSpacing = 0, FontFamily = font, FontSize = 9.5 };
            tbl.Columns.Add(new TableColumn { Width = new GridLength(118) }); // ITEM
            tbl.Columns.Add(new TableColumn { Width = new GridLength(24) });  // QTY
            tbl.Columns.Add(new TableColumn { Width = new GridLength(46) });  // UNIT
            tbl.Columns.Add(new TableColumn { Width = new GridLength(48) });  // TOTAL
            tbl.RowGroups.Add(new TableRowGroup());

            TableCell TC(string text, bool bold = false,
                         TextAlignment align = TextAlignment.Left,
                         bool bottomBorder = false)
            {
                Inline run = bold ? (Inline)new Bold(new Run(text)) : new Run(text);
                var cell = new TableCell(new Paragraph(run)
                {
                    Margin     = new Thickness(0, 1, 0, 1),
                    LineHeight = 13
                })
                {
                    TextAlignment = align,
                    Padding       = new Thickness(0, 2, 2, 2)
                };
                if (bottomBorder)
                {
                    cell.BorderBrush     = Brushes.Black;
                    cell.BorderThickness = new Thickness(0, 0, 0, 1);
                }
                return cell;
            }

            var hRow = new TableRow();
            hRow.Cells.Add(TC("ITEM",  bold: true, bottomBorder: true));
            hRow.Cells.Add(TC("QTY",   bold: true, align: TextAlignment.Center, bottomBorder: true));
            hRow.Cells.Add(TC("UNIT",  bold: true, align: TextAlignment.Right,  bottomBorder: true));
            hRow.Cells.Add(TC("TOTAL", bold: true, align: TextAlignment.Right,  bottomBorder: true));
            tbl.RowGroups[0].Rows.Add(hRow);

            foreach (var line in d.Lines)
            {
                var name = !string.IsNullOrWhiteSpace(line.ProductName)
                    ? line.ProductName
                    : $"Item #{line.ProductId}";
                var iRow = new TableRow();
                iRow.Cells.Add(TC(name));
                iRow.Cells.Add(TC(line.Quantity.ToString(), align: TextAlignment.Center));
                iRow.Cells.Add(TC($"{line.UnitPrice:N2}",  align: TextAlignment.Right));
                iRow.Cells.Add(TC($"{line.Subtotal:N2}",   align: TextAlignment.Right));
                tbl.RowGroups[0].Rows.Add(iRow);
            }
            fd.Blocks.Add(tbl);
            fd.Blocks.Add(Para(Dashes(), 8, TextAlignment.Center, fg: Brushes.Gray, spaceAfter: 2));

            // ── Totals ──
            // Totals table — fixed widths (Star/Auto unreliable in FlowDocument)
            var totTbl = new Table { CellSpacing = 0, FontFamily = font };
            totTbl.Columns.Add(new TableColumn { Width = new GridLength(164) }); // label
            totTbl.Columns.Add(new TableColumn { Width = new GridLength(72) });  // value
            totTbl.RowGroups.Add(new TableRowGroup());

            void TotRow(string label, string value, bool bold)
            {
                var r  = new TableRow();
                var fs = bold ? 11.0 : 10.0;
                Inline lInline = bold ? (Inline)new Bold(new Run(label)) : new Run(label);
                Inline vInline = bold ? (Inline)new Bold(new Run(value)) : new Run(value);
                r.Cells.Add(new TableCell(new Paragraph(lInline)
                    { Margin = new Thickness(0, 1, 0, 1), FontSize = fs }));
                r.Cells.Add(new TableCell(new Paragraph(vInline)
                    { Margin = new Thickness(0, 1, 0, 1), TextAlignment = TextAlignment.Right, FontSize = fs }));
                totTbl.RowGroups[0].Rows.Add(r);
            }
            TotRow("TOTAL",       $"{d.GrandTotal:N2}", true);
            TotRow("Amount Paid", $"{h.Amount:N2}",     false);
            fd.Blocks.Add(totTbl);

            fd.Blocks.Add(Para(Dashes(), 8, TextAlignment.Center, fg: Brushes.Gray, spaceAfter: 6));
            fd.Blocks.Add(Para("Thank you for your purchase!", 9, TextAlignment.Center, fg: Brushes.Gray));
            fd.Blocks.Add(Para("Please come again.", 9, TextAlignment.Center, fg: Brushes.Gray));

            return fd;
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
