#nullable enable
using System.ComponentModel;

namespace SihyuPOSPayroll.Models
{
    /// <summary>
    /// Represents the configuration state of a sidebar navigation module.
    /// Used by the Settings module to control which modules are visible to each user role.
    /// </summary>
    public class ModuleConfig : INotifyPropertyChanged
    {
        private bool _isEnabled;

        /// <summary>
        /// The name of the module (e.g., "Inventory", "Menu", "Orders").
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// Whether this module is enabled and should appear in the sidebar.
        /// Raises PropertyChanged when modified.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value == _isEnabled) return;
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        /// <summary>
        /// Whether this module is locked and cannot be disabled.
        /// True for "Settings" and "Dashboard" modules.
        /// </summary>
        public bool IsLocked { get; set; }

        // -------------- INotifyPropertyChanged --------------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
