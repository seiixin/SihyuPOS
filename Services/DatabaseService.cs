#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Thin facade kept for backward compatibility.
    /// All methods now delegate to the dedicated SQLite services.
    /// MySQL has been fully removed from this class.
    /// </summary>
    public class DatabaseService
    {
        // ── Authentication ────────────────────────────────────────────────────

        private readonly AuthService _authService = new AuthService();

        /// <summary>Delegates to <see cref="AuthService.Login"/>.</summary>
        public UserModel? AuthenticateUser(string email, string password) =>
            _authService.Login(email, password);

        // ── Menu ──────────────────────────────────────────────────────────────

        private readonly MenuService _menuService = new MenuService();

        /// <summary>Delegates to <see cref="MenuService.GetAllMenuItems"/>.</summary>
        public List<MenuModel> GetAllMenuItems() =>
            _menuService.GetAllMenuItems();

        // ── Inventory ─────────────────────────────────────────────────────────

        private readonly InventoryService _inventoryService = new InventoryService();

        /// <summary>Delegates to <see cref="InventoryService.GetAllItems"/>.</summary>
        public System.Collections.ObjectModel.ObservableCollection<InventoryItem> GetInventoryItems() =>
            _inventoryService.GetAllItems();
    }
}
