using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.Helpers;

namespace SihyuPOSPayroll.ViewModels
{
    public class MenuViewModel : BaseViewModel
    {
        private readonly MenuService _menuService;
        private MenuModel? _selectedMenuItem;
        private string _newItemName = string.Empty;
        private decimal _newItemPrice = 0;
        private string _newItemCategory = string.Empty;
        private string _newItemDescription = string.Empty;

        public MenuModel? SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                _selectedMenuItem = value;
                OnPropertyChanged();
                // Update the form fields when an item is selected
                if (value != null)
                {
                    NewItemName = value.Name ?? string.Empty;
                    NewItemPrice = value.Price ?? 0;
                    NewItemCategory = value.Category ?? string.Empty;
                    NewItemDescription = value.Description ?? string.Empty;
                }
            }
        }

        public string NewItemName
        {
            get => _newItemName;
            set
            {
                _newItemName = value;
                OnPropertyChanged();
            }
        }

        public decimal NewItemPrice
        {
            get => _newItemPrice;
            set
            {
                _newItemPrice = value;
                OnPropertyChanged();
            }
        }

        public string NewItemCategory
        {
            get => _newItemCategory;
            set
            {
                _newItemCategory = value;
                OnPropertyChanged();
            }
        }

        public string NewItemDescription
        {
            get => _newItemDescription;
            set
            {
                _newItemDescription = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<MenuModel> MenuItems { get; set; }

        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearFormCommand { get; }

        public MenuViewModel()
        {
            _menuService = new MenuService();
            MenuItems = new ObservableCollection<MenuModel>();

            LoadMenuItems();

            // Initialize commands using RelayCommand<object> to match the XAML binding
            AddCommand = new RelayCommand<object>(_ => AddMenuItem(), _ => CanAddMenuItem());
            UpdateCommand = new RelayCommand<object>(_ => UpdateMenuItem(), _ => CanUpdateMenuItem());
            DeleteCommand = new RelayCommand<object>(_ => DeleteMenuItem(), _ => CanDeleteMenuItem());
            ClearFormCommand = new RelayCommand<object>(_ => ClearForm());
        }

        public void LoadMenuItems()
        {
            try
            {
                var items = _menuService.GetAllMenuItems();
                MenuItems.Clear();
                foreach (var item in items)
                {
                    MenuItems.Add(item);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to load menu items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanAddMenuItem()
        {
            return !string.IsNullOrWhiteSpace(NewItemName) && NewItemPrice > 0;
        }

        private bool CanUpdateMenuItem()
        {
            return SelectedMenuItem != null && !string.IsNullOrWhiteSpace(NewItemName) && NewItemPrice > 0;
        }

        private bool CanDeleteMenuItem()
        {
            return SelectedMenuItem != null;
        }

        private void AddMenuItem()
        {
            try
            {
                var newItem = new MenuModel
                {
                    Name = NewItemName,
                    Price = NewItemPrice,
                    Category = NewItemCategory,
                    Description = NewItemDescription,
                    CreatedAt = System.DateTime.Now
                };

                _menuService.AddMenuItem(newItem);
                LoadMenuItems();
                ClearForm();
                MessageBox.Show("Menu item added successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to add menu item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateMenuItem()
        {
            if (SelectedMenuItem == null) return;

            try
            {
                SelectedMenuItem.Name = NewItemName;
                SelectedMenuItem.Price = NewItemPrice;
                SelectedMenuItem.Category = NewItemCategory;
                SelectedMenuItem.Description = NewItemDescription;

                _menuService.UpdateMenuItem(SelectedMenuItem);
                LoadMenuItems();
                ClearForm();
                MessageBox.Show("Menu item updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to update menu item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteMenuItem()
        {
            if (SelectedMenuItem == null) return;

            var result = MessageBox.Show("Are you sure you want to delete this menu item?",
                                       "Confirm Delete",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _menuService.DeleteMenuItem(SelectedMenuItem.Id);
                    LoadMenuItems();
                    ClearForm();
                    MessageBox.Show("Menu item deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Failed to delete menu item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearForm()
        {
            NewItemName = string.Empty;
            NewItemPrice = 0;
            NewItemCategory = string.Empty;
            NewItemDescription = string.Empty;
            SelectedMenuItem = null;
        }
    }
}