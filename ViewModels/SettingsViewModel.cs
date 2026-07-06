#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.ViewModels
{
    /// <summary>
    /// ViewModel for the Settings module.
    /// Allows admin to select a system mode (RestaurantMode/StoreMode) and manage module visibility.
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private SystemMode _selectedMode;
        private string? _saveResultMessage;

        public SettingsViewModel()
        {
            // Load current mode from SettingsService
            _selectedMode = SettingsService.Instance.CurrentMode;

            // Initialize ModuleConfigs from SettingsService
            ModuleConfigs = new ObservableCollection<ModuleConfig>();
            LoadModuleConfigs();

            // Set locked modules (Settings and Dashboard cannot be disabled)
            var settingsModule = ModuleConfigs.FirstOrDefault(m => 
                string.Equals(m.ModuleName, "Settings", StringComparison.OrdinalIgnoreCase));
            if (settingsModule != null)
            {
                settingsModule.IsLocked = true;
            }

            var dashboardModule = ModuleConfigs.FirstOrDefault(m => 
                string.Equals(m.ModuleName, "Dashboard", StringComparison.OrdinalIgnoreCase));
            if (dashboardModule != null)
            {
                dashboardModule.IsLocked = true;
            }

            // Initialize SaveCommand — always executable; persists mode and module configs
            SaveCommand = new RelayCommand(_ =>
            {
                try
                {
                    SettingsService.Instance.Save(SelectedMode, ModuleConfigs);
                    SaveResultMessage = "Settings saved successfully.";
                }
                catch (Exception)
                {
                    SaveResultMessage = "Failed to save settings. Please try again.";
                }
            });
        }

        /// <summary>
        /// The currently selected system mode (RestaurantMode or StoreMode).
        /// Raises PropertyChanged when modified.
        /// When changed, auto-applies module enable rules per Requirement 2.2–2.4.
        /// </summary>
        public SystemMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (value == _selectedMode) return;
                _selectedMode = value;
                ApplyModeAutoRules(value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Observable collection of module configurations for the admin to manage.
        /// Each module can be enabled/disabled unless it is locked.
        /// </summary>
        public ObservableCollection<ModuleConfig> ModuleConfigs { get; }

        /// <summary>
        /// Message displayed to the user after a save operation (success or error).
        /// Raises PropertyChanged when modified.
        /// </summary>
        public string? SaveResultMessage
        {
            get => _saveResultMessage;
            private set
            {
                if (value == _saveResultMessage) return;
                _saveResultMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Command to save the current settings (mode and module configurations).
        /// Logic will be implemented in task 3.4.
        /// </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// Loads module configurations from SettingsService.
        /// Populates ModuleConfigs with the current enabled/disabled state of each module.
        /// </summary>
        private void LoadModuleConfigs()
        {
            // Define all configurable modules
            var allModules = new[]
            {
                "Attendance", "Dashboard", "Employees", "Inventory",
                "Menu", "Orders", "Payroll", "PayslipRequests",
                "Receipts", "Sales", "Tables", "Users", "Settings"
            };

            var visibility = SettingsService.Instance.ModuleVisibility;

            foreach (var moduleName in allModules)
            {
                bool isEnabled = visibility.ContainsKey(moduleName) && visibility[moduleName];

                ModuleConfigs.Add(new ModuleConfig
                {
                    ModuleName = moduleName,
                    IsEnabled = isEnabled,
                    IsLocked = false // Will be set to true for Settings/Dashboard after this loop
                });
            }
        }

        /// <summary>
        /// Applies the automatic module enable/disable rules that correspond to the selected mode.
        /// RestaurantMode (Req 2.2): forces Inventory, Menu, and Orders to IsEnabled = true.
        /// StoreMode (Req 2.3, 2.4): forces Menu to IsEnabled = false; all other modules are preserved.
        /// </summary>
        private void ApplyModeAutoRules(SystemMode mode)
        {
            if (mode == SystemMode.RestaurantMode)
            {
                // Requirement 2.2: auto-enable Inventory, Menu, and Orders
                var moduleNames = new[] { "Inventory", "Menu", "Orders" };
                foreach (var name in moduleNames)
                {
                    var module = ModuleConfigs.FirstOrDefault(m =>
                        string.Equals(m.ModuleName, name, StringComparison.OrdinalIgnoreCase));
                    if (module != null)
                        module.IsEnabled = true;
                }
            }
            else if (mode == SystemMode.StoreMode)
            {
                // Requirement 2.3: auto-disable Menu only
                // Requirement 2.4: all other module states are preserved
                var menuModule = ModuleConfigs.FirstOrDefault(m =>
                    string.Equals(m.ModuleName, "Menu", StringComparison.OrdinalIgnoreCase));
                if (menuModule != null)
                    menuModule.IsEnabled = false;
            }
        }

        // -------------- INotifyPropertyChanged --------------
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
