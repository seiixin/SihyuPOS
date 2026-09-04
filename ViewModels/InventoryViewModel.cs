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
        // Barcode scan action — 3-state, persisted in app_settings as an int:
        //   -1 = not set → ModalPrompt (default)
        //    1 = QuickAdd (+1 qty, no modal)
        //    2 = QuickUpdate (open edit modal)
        private int _barcodeAction = -1;

        public InventoryViewModel()
        {
            _inventoryService = new InventoryService();
            _inventoryService.InitializeDatabase();

            // Ensure app_settings table exists then load persisted column states
            AppSettingsService.EnsureTable();
            _showQty       = AppSettingsService.GetColumnVisibility("inventory", "qty");
            _showCategory  = AppSettingsService.GetColumnVisibility("inventory", "category");
            _showExpiry    = AppSettingsService.GetColumnVisibility("inventory", "expiry_date");
            // -1 = not set (ModalPrompt default), 1 = QuickAdd, 2 = QuickUpdate
            _barcodeAction = AppSettingsService.GetIntSetting("inventory", "barcode_scan_action", defaultValue: -1);

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

        // ── Barcode scan action (persisted per-page, not global) ───────────────

        // Convention: -1 = not set (ModalPrompt), 1 = QuickAdd, 2 = QuickUpdate
        private void SetBarcodeAction(int value)
        {
            if (_barcodeAction == value) return;
            _barcodeAction = value;
            OnPropertyChanged(nameof(BarcodeActionIsDefault));
            OnPropertyChanged(nameof(BarcodeActionIsQuickAdd));
            OnPropertyChanged(nameof(BarcodeActionIsQuickUpdate));
            AppSettingsService.SetIntSetting("inventory", "barcode_scan_action", value);
        }

        /// <summary>True when no scan action has been set (ModalPrompt default).</summary>
        public bool BarcodeActionIsDefault
        {
            get => _barcodeAction == -1;
            set { if (value) SetBarcodeAction(-1); }
        }

        /// <summary>Silently add +1 qty on scan.</summary>
        public bool BarcodeActionIsQuickAdd
        {
            get => _barcodeAction == 1;
            set { if (value) SetBarcodeAction(1); }
        }

        /// <summary>Open pre-filled Edit modal on scan.</summary>
        public bool BarcodeActionIsQuickUpdate
        {
            get => _barcodeAction == 2;
            set { if (value) SetBarcodeAction(2); }
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
                        CenterBannerService.Success("Item Updated", $"'{item.ProductName}' saved successfully.");
                    }
                    else
                    {
                        ToastService.Error("Failed to update item.");
                    }
                }
                catch (Exception ex)
                {
                    ToastService.Error($"Update error: {ex.Message}");
                }
            };
            CurrentDialog = dialog;
        }

        private void DeleteItem(InventoryItem item)
        {
            if (item == null) return;

            // Confirmation is intentionally still a blocking dialog — the user
            // must explicitly confirm a destructive action before it executes.
            var result = MessageBox.Show(
                $"Delete '{item.ProductName}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (_inventoryService.DeleteItem(item.Id))
                {
                    RefreshData();
                    CenterBannerService.Warning("Item Deleted", $"'{item.ProductName}' removed from inventory.");
                }
                else
                {
                    ToastService.Error("Failed to delete item.");
                }
            }
            catch (Exception ex)
            {
                ToastService.Error($"Delete error: {ex.Message}");
            }
        }

        public void RefreshData() => LoadInventoryItems();

        // ── Quick-Add barcode chain ────────────────────────────────────────────
        // Callback invoked by Inventory.xaml.cs — returns the hint text to show
        // in the barcode strip while the lookup is running / finished.
        // Format: (hintText, isError)
        public event Action<string, bool>? LookupStatusChanged;

        /// <summary>
        /// Barcode scan entry point — called from Inventory.xaml.cs on Enter.
        ///
        /// For EXISTING inventory items the behaviour depends on BarcodeScanAction:
        ///   QuickAdd     — silent +1 stock increment, no modal
        ///   QuickUpdate  — opens the full Edit modal pre-filled
        ///   ModalPrompt  — shows the compact BarcodeActionDialog with two buttons
        ///
        /// For NEW barcodes (not yet in inventory) falls through to the 3-step
        /// lookup chain: Local JSON → Open Food Facts → Manual entry.
        /// </summary>
        public async Task QuickAddByBarcodeAsync(string barcode)
        {
            string bc = barcode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(bc)) return;

            if (CurrentDialog != null) return;
            if (IsLookingUp) return;

            // ── Check inventory first ────────────────────────────────────────
            var existing = _inventoryService.GetItemByBarcode(bc);
            if (existing != null)
            {
                // Read action from local VM property (persisted per page via AppSettingsService)
                if (_barcodeAction == 1)
                {
                    HandleQuickAddExisting(existing);
                }
                else if (_barcodeAction == 2)
                {
                    HandleQuickUpdateExisting(existing);
                }
                else
                {
                    // -1 or any unset value → ModalPrompt (default)
                    HandleModalPrompt(existing);
                }
                return;
            }

            // ── New barcode — 3-step lookup chain ────────────────────────────
            QuickAddPrefill prefill;

            // Step 1 — Local JSON
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

            // Step 2 — Open Food Facts API
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

            // Step 3 — Manual entry fallback
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

        // ── Existing-item scan handlers ───────────────────────────────────────

        /// <summary>QuickAdd — silently increments stock by +1, no modal.</summary>
        private void HandleQuickAddExisting(InventoryItem item)
        {
            int newQty = _inventoryService.IncrementItemQuantity(item.Id, delta: 1);
            if (newQty >= 0)
            {
                item.Quantity = newQty;
                RefreshData();
                CenterBannerService.Success("Stock Updated", $"+1 added — {item.ProductName}  (stock: {newQty})");
                LookupStatusChanged?.Invoke($"+1 → {item.ProductName} (stock: {newQty})", false);
            }
            else
            {
                ToastService.Error($"Failed to update stock for '{item.ProductName}'.");
                LookupStatusChanged?.Invoke("Failed to update stock.", true);
            }
        }

        /// <summary>QuickUpdate — opens the full Edit modal pre-filled with item details.</summary>
        private void HandleQuickUpdateExisting(InventoryItem item)
        {
            LookupStatusChanged?.Invoke($"Edit: {item.ProductName}", false);
            EditItem(item);
        }

        /// <summary>ModalPrompt — shows the compact BarcodeActionDialog.</summary>
        private void HandleModalPrompt(InventoryItem item)
        {
            LookupStatusChanged?.Invoke($"Found: {item.ProductName}", false);

            var dialog = new BarcodeActionDialog(item);
            dialog.ActionChosen += (_, action) =>
            {
                CurrentDialog = null;
                LookupStatusChanged?.Invoke(string.Empty, false);

                switch (action)
                {
                    case BarcodeActionDialog.BarcodeDialogAction.AddStock:
                        HandleQuickAddExisting(item);
                        break;

                    case BarcodeActionDialog.BarcodeDialogAction.Edit:
                        HandleQuickUpdateExisting(item);
                        break;

                    // Cancel — do nothing
                }
            };
            CurrentDialog = dialog;
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
                    CenterBannerService.Success("Item Added", $"'{newItem.ProductName}' saved to inventory.");
                }
                else
                {
                    ToastService.Error("Failed to add item.");
                }
            }
            catch (Exception ex)
            {
                ToastService.Error($"Add error: {ex.Message}");
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
