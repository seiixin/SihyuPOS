using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.Views.Admin.Inventory;

namespace SihyuPOSPayroll.ViewModels
{
    public class InventoryViewModel : INotifyPropertyChanged
    {
        private readonly InventoryService _inventoryService;
        private ObservableCollection<InventoryItem> _filteredItems = new();
        private ObservableCollection<InventoryItem> _inventoryItems = new();
        private string _searchText = string.Empty;
        private InventoryItem? _selectedItem;
        private object? _currentDialog;
        private bool _isLookingUp;
        private bool _showQty      = true;
        private bool _showExpiry   = true;
        private bool _showCategory = true;

        public InventoryViewModel()
        {
            _inventoryService = new InventoryService();
            _inventoryService.InitializeDatabase();

            // Ensure app_settings table exists then load persisted column states
            AppSettingsService.EnsureTable();
            _showQty      = AppSettingsService.GetColumnVisibility("inventory", "qty");
            _showCategory = AppSettingsService.GetColumnVisibility("inventory", "category");
            _showExpiry   = AppSettingsService.GetColumnVisibility("inventory", "expiry_date");

            AddItemCommand    = new DelegateCommand(AddItem);
            EditItemCommand   = new DelegateCommand<InventoryItem>(EditItem);
            DeleteItemCommand = new DelegateCommand<InventoryItem>(DeleteItem);
            RefreshCommand    = new DelegateCommand(RefreshData);

            LoadInventoryItems();
        }

        /// <summary>True while the barcode lookup chain is running (shows spinner in UI).</summary>
        public bool IsLookingUp
        {
            get => _isLookingUp;
            set { _isLookingUp = value; OnPropertyChanged(); }
        }

        /// <summary>Toggles the Qty column visibility in the inventory table.</summary>
        public bool ShowQty
        {
            get => _showQty;
            set
            {
                _showQty = value;
                OnPropertyChanged();
                AppSettingsService.SetColumnVisibility("inventory", "qty", value);
            }
        }

        /// <summary>Toggles the Expiry Date column visibility in the inventory table.</summary>
        public bool ShowExpiry
        {
            get => _showExpiry;
            set
            {
                _showExpiry = value;
                OnPropertyChanged();
                AppSettingsService.SetColumnVisibility("inventory", "expiry_date", value);
            }
        }

        /// <summary>Toggles the Category column visibility in the inventory table.</summary>
        public bool ShowCategory
        {
            get => _showCategory;
            set
            {
                _showCategory = value;
                OnPropertyChanged();
                AppSettingsService.SetColumnVisibility("inventory", "category", value);
            }
        }

        /// <summary>True when the system is running in StoreMode.</summary>
        public bool IsStoreMode => SettingsService.Instance.CurrentMode == SystemMode.StoreMode;

        /// <summary>True when the system is running in RestaurantMode.</summary>
        public bool IsRestaurantMode => !IsStoreMode;

        public ObservableCollection<InventoryItem> FilteredItems
        {
            get => _filteredItems;
            set { _filteredItems = value; OnPropertyChanged(); }
        }

        public ObservableCollection<InventoryItem> InventoryItems
        {
            get => _inventoryItems;
            set { _inventoryItems = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterItems(); }
        }

        public InventoryItem? SelectedItem
        {
            get => _selectedItem;
            set { _selectedItem = value; OnPropertyChanged(); }
        }

        public object? CurrentDialog
        {
            get => _currentDialog;
            set { _currentDialog = value; OnPropertyChanged(); }
        }

        public ICommand AddItemCommand    { get; }
        public ICommand EditItemCommand   { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand RefreshCommand    { get; }

        private void LoadInventoryItems()
        {
            try
            {
                FilteredItems  = _inventoryService.GetAllItems();
                InventoryItems = _inventoryService.GetExpiringItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading inventory items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterItems()
        {
            try
            {
                FilteredItems = _inventoryService.SearchItems(SearchText);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error filtering items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddItem()
        {
            var dialog = new AddEditInventoryDialog();
            dialog.DialogClosed += (sender, saved) =>
            {
                CurrentDialog = null;
                if (!saved) return;
                if (sender is not AddEditInventoryDialog src) return;
                PersistNewItem(src);
            };
            CurrentDialog = dialog;
        }

        private void EditItem(InventoryItem item)
        {
            if (item == null) return;

            var dialog = new AddEditInventoryDialog(item);
            dialog.DialogClosed += (sender, saved) =>
            {
                CurrentDialog = null;
                if (!saved) return;
                if (sender is not AddEditInventoryDialog src) return;

                item.Barcode      = src.Barcode;
                item.ProductName  = src.ProductName;
                item.CategoryName = src.CategoryName;
                item.Quantity     = src.Quantity;
                item.Price        = src.Price;
                item.ExpiryDate   = src.ExpiryDate;
                item.ImagePath    = src.ImagePath;

                try
                {
                    if (_inventoryService.UpdateItem(item))
                    {
                        RefreshData();
                        MessageBox.Show("Item updated successfully!", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to update item.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating item: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            CurrentDialog = dialog;
        }

        private void DeleteItem(InventoryItem item)
        {
            if (item == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{item.ProductName}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (_inventoryService.DeleteItem(item.Id))
                {
                    RefreshData();
                    MessageBox.Show("Item deleted successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to delete item.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting item: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RefreshData() => LoadInventoryItems();

        // ── Quick-Add barcode chain ────────────────────────────────────────────
        // Callback invoked by Inventory.xaml.cs — returns the hint text to show
        // in the barcode strip while the lookup is running / finished.
        // Format: (hintText, isError)
        public event Action<string, bool>? LookupStatusChanged;

        /// <summary>
        /// 3-step barcode lookup:
        ///   1. Local JSON (ph_grocery_starter.json)
        ///   2. Open Food Facts REST API (cloud fallback)
        ///   3. Manual entry modal if everything else fails / is offline
        /// Must be called from the UI thread.
        /// </summary>
        public async Task QuickAddByBarcodeAsync(string barcode)
        {
            string bc = barcode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(bc)) return;

            // ── Guard: already in a dialog ───────────────────────────────────
            if (CurrentDialog != null) return;

            // ── Guard: duplicate scan while lookup is running ────────────────
            if (IsLookingUp) return;

            QuickAddPrefill prefill;

            // ────────────────────────────────────────────────────────────────
            // Step 1 — Local JSON
            // ────────────────────────────────────────────────────────────────
            var localHit = BarcodeLookupService.Lookup(bc);
            if (localHit != null)
            {
                string? guessedCat = AddEditInventoryDialog.GuessCategory(localHit.Brand, localHit.ProductName);
                prefill = new QuickAddPrefill
                {
                    Barcode     = bc,
                    ProductName = localHit.ProductName,
                    Brand       = localHit.Brand,
                    Category    = guessedCat,
                    Source      = QuickAddPrefill.PrefillSource.LocalJson,
                };
                LookupStatusChanged?.Invoke($"Found: {localHit.ProductName}", false);
                OpenQuickAddDialog(prefill);
                return;
            }

            // ────────────────────────────────────────────────────────────────
            // Step 2 — Open Food Facts API
            // ────────────────────────────────────────────────────────────────
            IsLookingUp = true;
            LookupStatusChanged?.Invoke("Searching online…", false);

            OFFLookupResult offResult;
            try
            {
                offResult = await OpenFoodFactsService.LookupAsync(bc).ConfigureAwait(true);
            }
            finally
            {
                IsLookingUp = false;
            }

            if (offResult.IsFound)
            {
                prefill = new QuickAddPrefill
                {
                    Barcode     = bc,
                    ProductName = offResult.ProductName,
                    Brand       = offResult.Brand,
                    Category    = offResult.Category,
                    Source      = QuickAddPrefill.PrefillSource.OpenFoodFacts,
                };
                LookupStatusChanged?.Invoke($"Found: {offResult.ProductName}", false);
                OpenQuickAddDialog(prefill);
                return;
            }

            // ────────────────────────────────────────────────────────────────
            // Step 3 — Manual entry fallback
            // ────────────────────────────────────────────────────────────────
            string hint = offResult.Status switch
            {
                OFFLookupResult.LookupStatus.Offline  => "Offline — enter manually",
                OFFLookupResult.LookupStatus.Timeout  => "Lookup timed out — enter manually",
                OFFLookupResult.LookupStatus.NotFound => "Not found — enter manually",
                _                                     => "Not found — enter manually",
            };
            LookupStatusChanged?.Invoke(hint, true);

            prefill = new QuickAddPrefill
            {
                Barcode = bc,
                Source  = QuickAddPrefill.PrefillSource.ManualEntry,
            };
            OpenQuickAddDialog(prefill);
        }

        private void OpenQuickAddDialog(QuickAddPrefill prefill)
        {
            var dialog = new AddEditInventoryDialog(prefill);
            dialog.DialogClosed += (sender, saved) =>
            {
                CurrentDialog = null;
                LookupStatusChanged?.Invoke(string.Empty, false);
                if (!saved) return;
                if (sender is not AddEditInventoryDialog src) return;
                PersistNewItem(src);
            };
            CurrentDialog = dialog;
        }

        private void PersistNewItem(AddEditInventoryDialog src)
        {
            var newItem = new InventoryItem
            {
                Barcode      = src.Barcode,
                ProductName  = src.ProductName,
                CategoryName = src.CategoryName,
                Quantity     = src.Quantity,
                Price        = src.Price,
                ExpiryDate   = src.ExpiryDate,
                ImagePath    = src.ImagePath,
            };

            try
            {
                if (_inventoryService.AddItem(newItem))
                {
                    RefreshData();
                    MessageBox.Show("Item added successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to add item.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding item: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Legacy shim (kept so any remaining callers still compile) ─────────
        [Obsolete("Use QuickAddByBarcodeAsync instead.")]
        public void AddItemWithBarcode(string barcode) =>
            _ = QuickAddByBarcodeAsync(barcode);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        public DelegateCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }

    public class DelegateCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T>? _canExecute;
        public DelegateCommand(Action<T> execute, Predicate<T>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null) return true;
            if (parameter is T typed) return _canExecute(typed);
            return _canExecute(default!);
        }
        public void Execute(object? parameter)
        {
            if (parameter is T typed) _execute(typed);
            else _execute(default!);
        }
    }
}
