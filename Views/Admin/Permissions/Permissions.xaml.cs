using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SihyuPOSPayroll.Views.Admin.Permissions
{
    /// <summary>
    /// View model helper — adds a computed UserCount and ModuleCount for display.
    /// </summary>
    internal class RoleRow
    {
        public int    Id          { get; set; }
        public string Name        { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int    UserCount   { get; set; }
        public int    ModuleCount { get; set; }

        /// <summary>e.g. "5 modules" or "All modules" for display.</summary>
        public string ModuleSummary => ModuleCount == 0
            ? "No access"
            : ModuleCount >= 17 ? "Full access"
            : $"{ModuleCount} modules";

        public static RoleRow From(RoleModel m, int userCount, int moduleCount) => new()
        {
            Id          = m.Id,
            Name        = m.Name,
            Description = m.Description,
            UserCount   = userCount,
            ModuleCount = moduleCount,
        };

        public RoleModel ToModel() => new() { Id = Id, Name = Name, Description = Description };
    }

    public partial class Permissions : UserControl
    {
        private readonly RoleService _service = new();
        private List<RoleRow> _allRows = new();

        public Permissions()
        {
            InitializeComponent();
            ToastService.Register(PermissionsToast);
            LoadRoles();
        }

        // ── Data ─────────────────────────────────────────────────────────────

        private void LoadRoles()
        {
            try
            {
                var roles = _service.GetAllRoles();
                _allRows = roles
                    .Select(r => RoleRow.From(
                        r,
                        _service.CountUsersWithRole(r.Name),
                        _service.GetModulesForRole(r.Name).Count))
                    .ToList();
                ApplyFilter(SearchBox?.Text ?? string.Empty);
            }
            catch (Exception ex)
            {
                ToastService.Error("Failed to load roles: " + ex.Message);
            }
        }

        private void ApplyFilter(string query)
        {
            query = query.Trim().ToLower();
            var list = string.IsNullOrWhiteSpace(query)
                ? _allRows
                : _allRows.Where(r =>
                      r.Name.ToLower().Contains(query) ||
                      r.Description.ToLower().Contains(query))
                  .ToList();

            RolesDataGrid.ItemsSource = list;
            EmptyState.Visibility = list.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ── Overlay ───────────────────────────────────────────────────────────

        private void OpenDialog(AddEditRole dialog)
        {
            dialog.DialogClosed += (_, saved) =>
            {
                DialogOverlay.Visibility = Visibility.Collapsed;
                DialogHost.Content       = null;
                if (saved) LoadRoles();
            };
            DialogHost.Content       = dialog;
            DialogOverlay.Visibility = Visibility.Visible;
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter(SearchBox.Text);

        private void AddRole_Click(object sender, RoutedEventArgs e)
            => OpenDialog(new AddEditRole());

        private void EditRole_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RoleRow row)
                OpenDialog(new AddEditRole(row.ToModel()));
        }

        private void DeleteRole_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not RoleRow row)
                return;

            int inUse = _service.CountUsersWithRole(row.Name);
            if (inUse > 0)
            {
                ToastService.Warning(
                    $"Cannot delete '{row.Name}' — {inUse} user(s) are assigned to it.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete role '{row.Name}'?\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            if (_service.DeleteRole(row.Id, row.Name))
            {
                LoadRoles();
                ToastService.Info($"Role '{row.Name}' deleted.");
            }
            else
            {
                ToastService.Error("Failed to delete role.");
            }
        }
    }
}
