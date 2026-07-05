using SihyuPOSPayroll.Helpers; // RelayCommand
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace SihyuPOSPayroll.ViewModels
{
    public class SalesViewModel : BaseViewModel
    {
        public Array PeriodChoices => Enum.GetValues(typeof(ReportPeriod));

        private ReportPeriod _selected = ReportPeriod.Daily;
        public ReportPeriod SelectedPeriod
        {
            get => _selected;
            set { _selected = value; OnPropertyChanged(); }
        }

        public ObservableCollection<SalesRow> Rows { get; } = new();

        public decimal GrandTotal => Rows.Sum(r => r.TotalAmount);
        public int TotalReceipts => Rows.Sum(r => r.ReceiptCount);

        public ICommand GenerateCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportPdfCommand { get; }

        public SalesViewModel()
        {
            GenerateCommand = new RelayCommand(_ => Generate());
            ExportCsvCommand = new RelayCommand(_ => ExportCsv(), _ => Rows.Any());
            ExportPdfCommand = new RelayCommand(_ => ExportPdf(), _ => Rows.Any());

            Generate(); // initial load
        }

        private void Generate()
        {
            try
            {
                Rows.Clear();
                foreach (var r in SalesService.GetSales(SelectedPeriod))
                    Rows.Add(r);

                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(TotalReceipts));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate report.\n{ex.Message}", "Sales",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsv()
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"sales_{SelectedPeriod.ToString().ToLower()}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };
                if (sfd.ShowDialog() != true) return;

                var sb = new StringBuilder();
                sb.AppendLine("Period,From,To,Receipts,Total");
                foreach (var r in Rows)
                    sb.AppendLine($"{r.Period},{r.StartDate:yyyy-MM-dd},{r.EndDate:yyyy-MM-dd},{r.ReceiptCount},{r.TotalAmount:0.##}");

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("CSV exported.", "Sales", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export CSV.\n{ex.Message}", "Sales",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Uses Windows' “Microsoft Print to PDF” printer.
        private void ExportPdf()
        {
            try
            {
                var doc = new FlowDocument
                {
                    PagePadding = new Thickness(40),
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = 12
                };

                doc.Blocks.Add(new Paragraph(new Run($"Sales Report — {SelectedPeriod}"))
                {
                    FontSize = 18,
                    FontWeight = FontWeights.Bold
                });

                var table = new Table();
                doc.Blocks.Add(table);

                // columns
                foreach (var _ in Enumerable.Range(0, 5)) table.Columns.Add(new TableColumn());

                var header = new TableRowGroup();
                var body = new TableRowGroup();
                table.RowGroups.Add(header);
                table.RowGroups.Add(body);

                // header row
                header.Rows.Add(new TableRow());
                header.Rows[0].Cells.Add(new TableCell(new Paragraph(new Run("Period"))) { FontWeight = FontWeights.Bold });
                header.Rows[0].Cells.Add(new TableCell(new Paragraph(new Run("From"))) { FontWeight = FontWeights.Bold });
                header.Rows[0].Cells.Add(new TableCell(new Paragraph(new Run("To"))) { FontWeight = FontWeights.Bold });
                header.Rows[0].Cells.Add(new TableCell(new Paragraph(new Run("Receipts"))) { FontWeight = FontWeights.Bold });
                header.Rows[0].Cells.Add(new TableCell(new Paragraph(new Run("Total"))) { FontWeight = FontWeights.Bold });

                // data rows
                foreach (var r in Rows)
                {
                    var tr = new TableRow();
                    tr.Cells.Add(new TableCell(new Paragraph(new Run(r.Period))));
                    tr.Cells.Add(new TableCell(new Paragraph(new Run(r.StartDate.ToString("yyyy-MM-dd")))));
                    tr.Cells.Add(new TableCell(new Paragraph(new Run(r.EndDate.ToString("yyyy-MM-dd")))));
                    tr.Cells.Add(new TableCell(new Paragraph(new Run(r.ReceiptCount.ToString()))));
                    tr.Cells.Add(new TableCell(new Paragraph(new Run(r.TotalAmount.ToString("0.##")))));
                    body.Rows.Add(tr);
                }

                // totals
                doc.Blocks.Add(new Paragraph(new Run($"Receipts: {TotalReceipts}    Total: {GrandTotal:0.##}"))
                { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 0) });

                var dlg = new PrintDialog();
                if (dlg.ShowDialog() == true)
                {
                    // User can pick "Microsoft Print to PDF" to save as a PDF file
                    dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Sales Report");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export/print PDF.\n{ex.Message}", "Sales",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
