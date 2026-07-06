using System;
using System.Windows;
using System.Windows.Controls;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.Views.Admin.Tables
{
    public partial class Tables : UserControl
    {
        private TableViewModel Vm => (TableViewModel)DataContext;

        // Holds the id of the table being edited; 0 means "add new"
        private int _editingId = 0;

        public Tables()
        {
            InitializeComponent();
        }

        // ── Add ──────────────────────────────────────────────────────────────
        private void AddTable_Click(object sender, RoutedEventArgs e)
        {
            _editingId = 0;
            EditorTitle.Text = "Add Table";
            TableNumberBox.Text = string.Empty;
            EditorPanel.Visibility = Visibility.Visible;
            TableNumberBox.Focus();
        }

        // ── Edit ─────────────────────────────────────────────────────────────
        private void EditTable_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not SihyuPOSPayroll.Services.TableModel row) return;

            _editingId = row.Id;
            EditorTitle.Text = "Edit Table";
            TableNumberBox.Text = row.TableNumber;
            EditorPanel.Visibility = Visibility.Visible;
            TableNumberBox.Focus();
        }

        // ── Save ─────────────────────────────────────────────────────────────
        private void SaveEditor_Click(object sender, RoutedEventArgs e)
        {
            var number = TableNumberBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(number))
            {
                MessageBox.Show("Please enter a table number.", "Tables",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_editingId == 0)
                    TableService.AddTable(number);
                else
                    TableService.UpdateTable(_editingId, number);

                EditorPanel.Visibility = Visibility.Collapsed;
                Vm.LoadTables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save table.\n{ex.Message}", "Tables",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Cancel ───────────────────────────────────────────────────────────
        private void CancelEditor_Click(object sender, RoutedEventArgs e)
        {
            EditorPanel.Visibility = Visibility.Collapsed;
        }

        // ── Delete ───────────────────────────────────────────────────────────
        private void DeleteTable_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not SihyuPOSPayroll.Services.TableModel row) return;

            var confirm = MessageBox.Show(
                $"Delete table \"{row.TableNumber}\"?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                TableService.DeleteTable(row.Id);
                Vm.LoadTables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete table.\n{ex.Message}", "Tables",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
