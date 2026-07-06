using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
        private ObservableCollection<InventoryItem> _filteredItems;
        private ObservableCollection<InventoryItem> _inventoryItems;
        private string _searchText = string.Empty;
        private InventoryItem _selectedItem;
        private object _currentDialog;

        public InventoryViewModel()
        {
            _inventoryService = new InventoryService();
            _inventoryService.InitializeDatabase();

            AddItemCommand = new DelegateCommand(AddItem);
            EditItemCommand = new DelegateCommand<InventoryItem>(EditItem);
            DeleteItemCommand = new DelegateCommand<InventoryItem>(DeleteItem);
            RefreshCommand = new DelegateCommand(RefreshData);

            LoadInventoryItems();
        }

        /// <summary>
        /// True when the system is running in StoreMode.
        /// Used to hide expiry-related UI that only applies to RestaurantMode.
        /// </summary>
        public bool IsStoreMode => SettingsService.Instance.CurrentMode == SystemMode.StoreMode;

        /// <summary>
        /// True when the system is running in RestaurantMode.
        /// Used to show expiry-related UI.
        /// </summary>
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

        public InventoryItem SelectedItem
        {
            get => _selectedItem;
            set { _selectedItem = value; OnPropertyChanged(); }
        }

        public object CurrentDialog
        {
            get => _currentDialog;
            set { _currentDialog = value; OnPropertyChanged(); }
        }

        public ICommand AddItemCommand { get; }
        public ICommand EditItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand RefreshCommand { get; }

        private void LoadInventoryItems()
        {
            try
            {
                FilteredItems = _inventoryService.GetAllItems();
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

                var src = (AddEditInventoryDialog)sender;
                var newItem = new InventoryItem
                {
                    ProductName  = src.ProductName,
                    CategoryName = src.CategoryName,
                    Quantity     = src.Quantity,
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

                var src = (AddEditInventoryDialog)sender;
                item.ProductName  = src.ProductName;
                item.CategoryName = src.CategoryName;
                item.Quantity     = src.Quantity;
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

        private void RefreshData() => LoadInventoryItems();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Simple non-nullable delegate command (no overload ambiguity)
    public class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public DelegateCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }

    public class DelegateCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;
        public DelegateCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
        public void Execute(object parameter) => _execute((T)parameter);
    }
}
