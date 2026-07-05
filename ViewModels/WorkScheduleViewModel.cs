using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using MySql.Data.MySqlClient;

namespace SihyuPOSPayroll.ViewModels
{
    public class WorkScheduleViewModel : INotifyPropertyChanged
    {
        private readonly WorkScheduleService _service;

        public ObservableCollection<WorkScheduleModel> Schedules { get; } = new();

        private WorkScheduleModel? _selected;
        public WorkScheduleModel? Selected
        {
            get => _selected;
            set { _selected = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                RefreshCanExec();
            }
        }

        private string? _status;
        public string? Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }

        public RelayCommand ReloadCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand<WorkScheduleModel> RemoveCommand { get; }

        public event Action? RequestClose;

        public WorkScheduleViewModel(WorkScheduleService? service = null)
        {
            _service = service ?? new WorkScheduleService();

            ReloadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => AddNew());
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
            RemoveCommand = new RelayCommand<WorkScheduleModel>(
                item => Remove(item),
                item => item != null && !IsBusy
            );

            Schedules.CollectionChanged += (_, __) => RefreshCanExec();
        }

        private bool CanSave() => Schedules.Count > 0 && !IsBusy;

        private void RefreshCanExec()
        {
            try
            {
                SaveCommand.RaiseCanExecuteChanged();
                RemoveCommand.RaiseCanExecuteChanged();
            }
            catch
            {
                // fallback
            }
        }

        public void Load()
        {
            IsBusy = true;
            Status = "Loading…";

            try
            {
                if (!_service.TryEnsureSchema())
                {
                    Status = "Schema not ready";
                    MessageBox.Show("Work Schedules schema error:\n" + (_service.LastError ?? "Unknown error"),
                        "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!_service.IsDbReady())
                {
                    Status = "DB not accessible";
                    MessageBox.Show("Work Schedules table not accessible:\n" + (_service.LastError ?? "Unknown error"),
                        "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Schedules.Clear();
                foreach (var r in _service.Load())
                    Schedules.Add(r);

                Status = $"Loaded {Schedules.Count} item(s).";
            }
            catch (MySqlException mex)
            {
                Status = "DB error";
                MessageBox.Show($"Database error (#{mex.Number}): {mex.Message}",
                    "Work Schedules", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Status = "Failed to load.";
                MessageBox.Show($"Failed to load schedules:\n{ex.Message}",
                    "Work Schedules", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                RefreshCanExec();
            }
        }

        private void AddNew()
        {
            var item = new WorkScheduleModel
            {
                Label = string.Empty,
                Mon = true,
                Tue = true,
                Wed = true,
                Thu = true,
                Fri = true,
                Sat = false,
                Sun = false,
                IsActive = true,
                UpdatedAt = DateTime.Now
            };
            Schedules.Add(item);
            Selected = item;
            Status = "New row added.";
            RefreshCanExec();
        }

        private void Save()
        {
            try
            {
                IsBusy = true;

                var invalid = Schedules.Where(r =>
                        string.IsNullOrWhiteSpace(r.Label) ||
                        !(r.Mon || r.Tue || r.Wed || r.Thu || r.Fri || r.Sat || r.Sun))
                    .ToList();

                if (invalid.Count > 0)
                {
                    MessageBox.Show(
                        "Please ensure each row has a Label and at least one day selected.",
                        "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var r in Schedules)
                    r.UpdatedAt = DateTime.Now;

                _service.Save(Schedules);

                Status = $"Saved {Schedules.Count} item(s) at {DateTime.Now:HH:mm}.";
                MessageBox.Show("Saved successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (MySqlException mex)
            {
                Status = "DB error";
                MessageBox.Show($"Save failed (DB #{mex.Number}): {mex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Status = "Save failed.";
                MessageBox.Show($"Failed to save:\n{ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                RefreshCanExec();
            }
        }

        private void Remove(WorkScheduleModel? item)
        {
            if (item is null) return;

            var confirm = MessageBox.Show(
                $"Remove “{item.Label}” from this list?\n(This does not delete from DB until you Save.)",
                "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            Schedules.Remove(item);
            if (ReferenceEquals(Selected, item)) Selected = null;
            Status = "Row removed. Save to apply.";
            RefreshCanExec();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
