using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SihyuPOSPayroll.Helpers;   // RelayCommand / RelayCommand<T>
using SihyuPOSPayroll.Services;
using MySql.Data.MySqlClient;

namespace SihyuPOSPayroll.ViewModels
{
    /// <summary>
    /// ViewModel for Positions & Salaries editor.
    /// - No DB calls in ctor (Load is invoked by the View on Loaded)
    /// - Save button enablement reacts to Busy state, collection changes, and row edits
    /// - Safe schema/access checks with clear error messages
    /// </summary>
    public sealed class PositionSalaryViewModel : INotifyPropertyChanged
    {
        private readonly PositionSalaryService _service;

        public ObservableCollection<PositionSalaryService.PositionSalary> Rates { get; } = new();

        private PositionSalaryService.PositionSalary? _selected;
        public PositionSalaryService.PositionSalary? Selected
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

        // Commands
        public RelayCommand ReloadCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand<PositionSalaryService.PositionSalary> DeactivateCommand { get; }
        public RelayCommand<PositionSalaryService.PositionSalary> RemoveCommand { get; }

        public event Action? RequestClose;

        public PositionSalaryViewModel(PositionSalaryService? service = null)
        {
            _service = service ?? new PositionSalaryService();

            ReloadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => AddNew());
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
            DeactivateCommand = new RelayCommand<PositionSalaryService.PositionSalary>(Deactivate, x => x is not null);
            RemoveCommand = new RelayCommand<PositionSalaryService.PositionSalary>(Remove, x => x is not null);

            // React to add/remove in the grid
            Rates.CollectionChanged += Rates_CollectionChanged;
        }

        private bool CanSave() => Rates.Count > 0 && !IsBusy;

        private void RefreshCanExec()
        {
            try { SaveCommand.RaiseCanExecuteChanged(); }
            catch { CommandManager.InvalidateRequerySuggested(); }
        }

        // ===== Core ops =====

        public void Load()
        {
            IsBusy = true;
            Status = "Loading…";

            try
            {
                // Safe schema + accessibility checks (non-throwing)
                if (!EnsureReadyOrWarn()) return;

                Rates.Clear();
                var rows = _service.Load();
                foreach (var r in rows)
                {
                    AttachRowHandlers(r);
                    Rates.Add(r);
                }

                if (!string.IsNullOrWhiteSpace(_service.LastError))
                {
                    MessageBox.Show($"Warning while loading:\n{_service.LastError}",
                        "Positions & Salaries", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                Status = $"Loaded {Rates.Count} item(s).";
            }
            catch (MySqlException mex)
            {
                Status = "DB error.";
                MessageBox.Show($"Database error (#{mex.Number}): {mex.Message}",
                    "Positions & Salaries", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Status = "Failed to load.";
                MessageBox.Show($"Failed to load positions:\n{ex.Message}",
                    "Positions & Salaries", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                RefreshCanExec();
            }
        }

        private void AddNew()
        {
            var item = new PositionSalaryService.PositionSalary
            {
                Position = string.Empty,
                DailyRate = 0m,
                IsActive = true,
                UpdatedAt = DateTime.Now
            };
            AttachRowHandlers(item);
            Rates.Add(item);
            Selected = item;
            Status = "New row added.";
            RefreshCanExec();
        }

        private void Save()
        {
            try
            {
                IsBusy = true;

                if (!EnsureReadyOrWarn()) return;

                // Basic validation
                var invalid = Rates.Where(r =>
                                string.IsNullOrWhiteSpace(r.Position) ||
                                r.DailyRate < 0m)
                            .ToList();

                if (invalid.Count > 0)
                {
                    MessageBox.Show("Please ensure each row has a Position and a non-negative Daily Rate.",
                        "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Stamp UpdatedAt so newest wins on upsert
                foreach (var r in Rates) r.UpdatedAt = DateTime.Now;

                // Service.Save() upserts provided rows AND deletes missing rows in DB
                _service.Save(Rates);

                Status = $"Saved {Rates.Count} item(s) at {DateTime.Now:HH:mm}.";
                MessageBox.Show("Saved successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (MySqlException mex)
            {
                Status = "DB error.";
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

        private void Deactivate(PositionSalaryService.PositionSalary? item)
        {
            if (item is null) return;
            item.IsActive = false;
            item.UpdatedAt = DateTime.Now;
            Status = $"Deactivated “{item.Position}”. Save to apply.";
            RefreshCanExec();
        }

        private void Remove(PositionSalaryService.PositionSalary? item)
        {
            if (item is null) return;

            var confirm = MessageBox.Show(
                $"Remove “{item.Position}” from this list?\n(This will be deleted from DB after you Save.)",
                "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            DetachRowHandlers(item);
            Rates.Remove(item);
            if (ReferenceEquals(Selected, item)) Selected = null;
            Status = "Row removed. Save to apply.";
            RefreshCanExec();
        }

        // ===== Helpers =====

        /// <summary>
        /// Ensures table exists and is accessible. Shows MessageBox on failures. Returns true if ready.
        /// </summary>
        private bool EnsureReadyOrWarn()
        {
            if (!_service.TryEnsureSchema())
            {
                Status = "Schema not ready.";
                MessageBox.Show($"Positions & Salaries schema error:\n{_service.LastError ?? "Unknown error."}",
                    "Database", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!_service.IsDbReady())
            {
                Status = "Table not accessible.";
                MessageBox.Show($"Positions & Salaries not accessible:\n{_service.LastError ?? "Unknown error."}",
                    "Database", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void Rates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (var it in e.NewItems.OfType<PositionSalaryService.PositionSalary>())
                    AttachRowHandlers(it);

            if (e.OldItems != null)
                foreach (var it in e.OldItems.OfType<PositionSalaryService.PositionSalary>())
                    DetachRowHandlers(it);

            RefreshCanExec();
        }

        private void AttachRowHandlers(PositionSalaryService.PositionSalary item)
        {
            item.PropertyChanged += Row_PropertyChanged;
        }

        private void DetachRowHandlers(PositionSalaryService.PositionSalary item)
        {
            item.PropertyChanged -= Row_PropertyChanged;
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Any edit should enable Save and update status hint
            if (e.PropertyName is nameof(PositionSalaryService.PositionSalary.Position)
                               or nameof(PositionSalaryService.PositionSalary.DailyRate)
                               or nameof(PositionSalaryService.PositionSalary.IsActive))
            {
                Status = "Edited. Click Save to persist.";
                RefreshCanExec();
            }
        }

        // ===== INotifyPropertyChanged =====
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
