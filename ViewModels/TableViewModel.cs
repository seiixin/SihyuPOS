using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

// Alias the service model so there's no ambiguity with any other TableModel
using TableDto = HillsCafeManagement.Services.TableModel;

namespace SihyuPOSPayroll.ViewModels
{
    public class TableViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<TableDto> Tables { get; } = new();

        private TableDto? _selected;
        public TableDto? Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand ForceAvailableCommand { get; }
        public ICommand ForceOccupiedCommand { get; }

        public TableViewModel()
        {
            RefreshCommand = new RelayCommand(_ => LoadTables());
            ForceAvailableCommand = new RelayCommand(_ => ForceAvailable(), _ => Selected != null);
            ForceOccupiedCommand = new RelayCommand(_ => ForceOccupied(), _ => Selected != null);

            LoadTables();
        }

        // Load all tables with current status
        public void LoadTables()
        {
            try
            {
                var data = TableService.GetAllTables(); // returns List<TableDto>
                Tables.Clear();
                foreach (var t in data) Tables.Add(t);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load tables.\n{ex.Message}", "Tables",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Optional manual overrides (not recommended, but available)
        private void ForceAvailable()
        {
            if (Selected == null) return;

            try
            {
                TableService.SetAvailabilityManual(Selected.Id, true);
                LoadTables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update table.\n{ex.Message}", "Tables",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ForceOccupied()
        {
            if (Selected == null) return;

            try
            {
                TableService.SetAvailabilityManual(Selected.Id, false);
                LoadTables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update table.\n{ex.Message}", "Tables",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // INotifyPropertyChanged
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
