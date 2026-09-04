#nullable enable
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SihyuPOSPayroll.Views.Admin.Permissions
{
    public partial class AddEditRole : UserControl
    {
        private readonly RoleService _service = new();
        private readonly bool        _isEdit;
        private readonly RoleModel?  _editing;
        private readonly string      _oldName;

        public event EventHandler<bool>? DialogClosed;

        // ── All module checkboxes in the XAML (Tag = module_key) ─────────────
        private IEnumerable<CheckBox> AllCheckBoxes =>
            new CheckBox[]
            {
                ChkDashboard, ChkInventory, ChkCategories,
                ChkUsers, ChkEmployees, ChkPositions,
                ChkPOS, ChkOrders, ChkReceipts,
            };

        // ── Constructor ───────────────────────────────────────────────────────

        public AddEditRole(RoleModel? role = null)
        {
            InitializeComponent();

            // Esc → cancel
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; Close(false); }
            };

            if (role != null)
            {
                _isEdit  = true;
                _editing = role;
                _oldName = role.Name;

                TitleText.Text    = "Edit Role";
                SubtitleText.Text = "Update the role name, description, and module access.";

                NameTextBox.Text        = role.Name;
                DescriptionTextBox.Text = role.Description;

                // Load saved module grants
                var granted = _service.GetModulesForRole(role.Name);
                foreach (var cb in AllCheckBoxes)
                    cb.IsChecked = granted.Contains(cb.Tag?.ToString() ?? string.Empty);
            }
            else
            {
                _oldName = string.Empty;
                TitleText.Text    = "Add Role";
                SubtitleText.Text = "Define a role name and choose which modules it can access.";

                // New role — nothing checked by default
                foreach (var cb in AllCheckBoxes)
                    cb.IsChecked = false;
            }
        }

        // ── Select / Clear All ────────────────────────────────────────────────

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in AllCheckBoxes) cb.IsChecked = true;
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in AllCheckBoxes) cb.IsChecked = false;
        }

        // ── Close ─────────────────────────────────────────────────────────────

        private void Close(bool saved) => DialogClosed?.Invoke(this, saved);

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);

        // ── Save ──────────────────────────────────────────────────────────────

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = NameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ToastService.Warning("Role Name is required.");
                NameTextBox.Focus();
                return;
            }

            var model = new RoleModel
            {
                Id          = _editing?.Id ?? 0,
                Name        = name,
                Description = DescriptionTextBox.Text.Trim(),
            };

            // Collect checked module keys
            var selectedKeys = AllCheckBoxes
                .Where(cb => cb.IsChecked == true)
                .Select(cb => cb.Tag?.ToString() ?? string.Empty)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToList();

            bool roleOk;
            if (_isEdit && _editing != null)
            {
                roleOk = _service.UpdateRole(model, _oldName);

                // If the name changed, SaveModulesForRole needs the NEW name
                if (roleOk)
                    _service.SaveModulesForRole(model.Name, selectedKeys);
            }
            else
            {
                roleOk = _service.AddRole(model);
                if (roleOk)
                    _service.SaveModulesForRole(model.Name, selectedKeys);
            }

            if (roleOk)
            {
                Close(true);
                ToastService.Success(_isEdit
                    ? $"Role '{model.Name}' updated."
                    : $"Role '{model.Name}' added.");
            }
            else
            {
                ToastService.Error(_isEdit
                    ? "Failed to update role."
                    : "Failed to add role. Name may already exist.");
            }
        }
    }
}
