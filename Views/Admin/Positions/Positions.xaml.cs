using SihyuPOSPayroll.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SihyuPOSPayroll.Views.Admin.Positions
{
    public partial class Positions : UserControl
    {
        private readonly PositionSalaryService _service = new();
        private List<PositionSalaryService.PositionSalary> _allItems = new();

        public Positions()
        {
            InitializeComponent();
            ToastService.Register(PositionsToast);
            LoadPositions();
        }

        // ── Data ─────────────────────────────────────────────────────────────

        private void LoadPositions()
        {
            try
            {
                _allItems = _service.Load();
                ApplyFilter(SearchBox?.Text ?? string.Empty);
            }
            catch (Exception ex)
            {
                ToastService.Error("Failed to load positions: " + ex.Message);
            }
        }

        private void ApplyFilter(string query)
        {
            query = query.Trim().ToLower();
            var list = string.IsNullOrWhiteSpace(query)
                ? _allItems
                : _allItems.Where(p =>
                      p.Position.ToLower().Contains(query))
                  .ToList();

            PositionsDataGrid.ItemsSource = list;
            EmptyState.Visibility = list.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ── Overlay ───────────────────────────────────────────────────────────

        private void OpenDialog(AddEditPosition dialog)
        {
            dialog.DialogClosed += (_, saved) =>
            {
                DialogOverlay.Visibility = Visibility.Collapsed;
                DialogHost.Content       = null;
                if (saved) LoadPositions();
            };
            DialogHost.Content       = dialog;
            DialogOverlay.Visibility = Visibility.Visible;
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter(SearchBox.Text);

        private void AddPosition_Click(object sender, RoutedEventArgs e)
            => OpenDialog(new AddEditPosition());

        private void EditPosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe &&
                fe.DataContext is PositionSalaryService.PositionSalary item)
                OpenDialog(new AddEditPosition(item));
        }

        private void DeletePosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe ||
                fe.DataContext is not PositionSalaryService.PositionSalary item)
                return;

            var confirm = MessageBox.Show(
                $"Delete position '{item.Position}'?\nEmployees using this position will keep their existing salary.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            if (_service.DeletePosition(item.Id))
            {
                LoadPositions();
                ToastService.Info($"Position '{item.Position}' deleted.");
            }
            else
            {
                ToastService.Error("Failed to delete position.");
            }
        }
    }
}
