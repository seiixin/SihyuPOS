#nullable enable
using MySql.Data.MySqlClient;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Singleton service responsible for persisting and retrieving SystemMode and
    /// ModuleVisibility configuration from MySQL. Call EnsureSchemaAtStartup() once
    /// in App.xaml.cs before any views are constructed, then call Instance.Load().
    /// </summary>
    public class SettingsService
    {
        // ----------------------------------------------------------------
        // Singleton
        // ----------------------------------------------------------------
        public static readonly SettingsService Instance = new SettingsService();
        private SettingsService() { }

        private const string DefaultCs =
            "server=localhost;user=root;password=;database=sihyu_pos;";
        private const int CommandTimeoutSeconds = 15;

        // ----------------------------------------------------------------
        // In-memory state
        // ----------------------------------------------------------------
        public SystemMode CurrentMode { get; private set; } = SystemMode.RestaurantMode;

        private Dictionary<string, bool> _visibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, bool> ModuleVisibility => _visibility;

        // ----------------------------------------------------------------
        // Events
        // ----------------------------------------------------------------
        /// <summary>
        /// Raised after a successful Save(); subscribers (e.g. SidebarViewModel) call
        /// InitializeMenuItems() to refresh their MenuGroups.
        /// </summary>
        public event Action? SettingsChanged;

        // ----------------------------------------------------------------
        // Schema migration – call ONCE at startup from App.xaml.cs
        // ----------------------------------------------------------------
        private static bool _schemaChecked = false;

        /// <summary>
        /// Creates all required tables if they do not exist and runs any pending
        /// column migrations. Safe to call more than once (idempotent).
        /// </summary>
        public static void EnsureSchemaAtStartup(string cs = DefaultCs)
        {
            if (_schemaChecked) return;

            try
            {
                using var conn = new MySqlConnection(cs);
                conn.Open();

                // --------------------------------------------------------
                // 1. SystemSettings table
                // --------------------------------------------------------
                ExecuteNonQuery(conn, @"
                    CREATE TABLE IF NOT EXISTS SystemSettings (
                        SettingKey   VARCHAR(100) NOT NULL PRIMARY KEY,
                        SettingValue VARCHAR(255) NOT NULL
                    );");

                // --------------------------------------------------------
                // 2. ModuleVisibility table
                // --------------------------------------------------------
                ExecuteNonQuery(conn, @"
                    CREATE TABLE IF NOT EXISTS ModuleVisibility (
                        ModuleName VARCHAR(100) NOT NULL PRIMARY KEY,
                        IsEnabled  TINYINT(1)   NOT NULL DEFAULT 1
                    );");

                // --------------------------------------------------------
                // 3. IngredientRecipes table
                // --------------------------------------------------------
                ExecuteNonQuery(conn, @"
                    CREATE TABLE IF NOT EXISTS IngredientRecipes (
                        Id                 INT           NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        MenuItemId         INT           NOT NULL,
                        IngredientId       INT           NOT NULL,
                        QuantityPerServing DECIMAL(10,4) NOT NULL,
                        UNIQUE KEY uq_recipe (MenuItemId, IngredientId),
                        CONSTRAINT fk_recipe_menu FOREIGN KEY (MenuItemId)   REFERENCES menu(id)            ON DELETE CASCADE,
                        CONSTRAINT fk_recipe_inv  FOREIGN KEY (IngredientId) REFERENCES inventory_items(Id) ON DELETE CASCADE
                    );");

                // --------------------------------------------------------
                // 4. IngredientBatches table
                // --------------------------------------------------------
                ExecuteNonQuery(conn, @"
                    CREATE TABLE IF NOT EXISTS IngredientBatches (
                        Id            INT           NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        IngredientId  INT           NOT NULL,
                        BatchQuantity DECIMAL(10,4) NOT NULL,
                        YieldPerBatch DECIMAL(10,4) NOT NULL,
                        CreatedAt     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT fk_batch_inv FOREIGN KEY (IngredientId) REFERENCES inventory_items(Id) ON DELETE CASCADE
                    );");

                // --------------------------------------------------------
                // 5. PackagingMaterials table
                // --------------------------------------------------------
                ExecuteNonQuery(conn, @"
                    CREATE TABLE IF NOT EXISTS PackagingMaterials (
                        PackagingMaterialId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                        MenuItemId          INT NOT NULL,
                        InventoryItemId     INT NOT NULL,
                        QuantityPerOrder    INT NOT NULL DEFAULT 1,
                        CONSTRAINT fk_pkg_menu FOREIGN KEY (MenuItemId)      REFERENCES menu(id)            ON DELETE CASCADE,
                        CONSTRAINT fk_pkg_inv  FOREIGN KEY (InventoryItemId) REFERENCES inventory_items(Id) ON DELETE RESTRICT
                    );");

                // --------------------------------------------------------
                // 6. Migrate orders table: add order_type column if missing
                // --------------------------------------------------------
                ExecuteNonQuery(conn, @"
                    ALTER TABLE orders
                        ADD COLUMN IF NOT EXISTS order_type
                        ENUM('DineIn','TakeOut','NotApplicable') NOT NULL DEFAULT 'NotApplicable';");

                // --------------------------------------------------------
                // 7. Seed SystemSettings — default SystemMode = RestaurantMode
                // --------------------------------------------------------
                ExecuteNonQuery(conn, @"
                    INSERT IGNORE INTO SystemSettings (SettingKey, SettingValue)
                    VALUES ('SystemMode', 'RestaurantMode');");

                // --------------------------------------------------------
                // 8. Seed ModuleVisibility — 13 modules, all enabled by default
                // --------------------------------------------------------
                string[] defaultModules =
                {
                    "Attendance", "Dashboard", "Employees", "Inventory",
                    "Menu", "Orders", "Payroll", "PayslipRequests",
                    "Receipts", "Sales", "Tables", "Users", "Settings"
                };

                foreach (var moduleName in defaultModules)
                {
                    using var cmd = new MySqlCommand(
                        "INSERT IGNORE INTO ModuleVisibility (ModuleName, IsEnabled) VALUES (@name, 1);",
                        conn);
                    cmd.CommandTimeout = CommandTimeoutSeconds;
                    cmd.Parameters.AddWithValue("@name", moduleName);
                    cmd.ExecuteNonQuery();
                }

                _schemaChecked = true;
            }
            catch (Exception ex)
            {
                // Log only; do NOT rethrow — other services must continue to work.
                Debug.WriteLine($"[SettingsService] EnsureSchemaAtStartup warning: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------
        // Load – populate in-memory cache from DB
        // ----------------------------------------------------------------
        /// <summary>
        /// Reads SystemMode and ModuleVisibility from the database into the
        /// in-memory cache. On any DB error, falls back to defaults
        /// (RestaurantMode, all 13 modules enabled) and logs via Debug.WriteLine.
        /// Must be called after EnsureSchemaAtStartup.
        /// </summary>
        public void Load(string cs = DefaultCs)
        {
            try
            {
                using var conn = new MySqlConnection(cs);
                conn.Open();

                // Read SystemMode
                using (var cmd = new MySqlCommand(
                    "SELECT SettingValue FROM SystemSettings WHERE SettingKey = 'SystemMode' LIMIT 1;",
                    conn))
                {
                    cmd.CommandTimeout = CommandTimeoutSeconds;
                    var raw = cmd.ExecuteScalar() as string;
                    CurrentMode = ParseSystemMode(raw);
                }

                // Read ModuleVisibility
                var visibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = new MySqlCommand(
                    "SELECT ModuleName, IsEnabled FROM ModuleVisibility;",
                    conn))
                {
                    cmd.CommandTimeout = CommandTimeoutSeconds;
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        string name = rdr.GetString(0);
                        bool enabled = rdr.GetBoolean(1);
                        visibility[name] = enabled;
                    }
                }

                // Ensure all 13 default modules are present in the cache (defensive)
                EnsureDefaultModulesInCache(visibility);
                _visibility = visibility;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Load error, using defaults: {ex.Message}");
                CurrentMode = SystemMode.RestaurantMode;
                _visibility = BuildDefaultVisibility();
            }
        }

        // ----------------------------------------------------------------
        // Save – persist mode and module states in a single transaction
        // ----------------------------------------------------------------
        /// <summary>
        /// Writes the selected mode and all module states to the database within a
        /// single transaction. On success, updates the in-memory cache and raises
        /// SettingsChanged. On failure, rolls back and does NOT update the cache.
        /// </summary>
        public void Save(
            SystemMode mode,
            IEnumerable<ModuleConfig> modules,
            string cs = DefaultCs)
        {
            using var conn = new MySqlConnection(cs);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // Persist SystemMode
                using (var cmd = new MySqlCommand(
                    @"INSERT INTO SystemSettings (SettingKey, SettingValue)
                      VALUES ('SystemMode', @val)
                      ON DUPLICATE KEY UPDATE SettingValue = @val;",
                    conn, tx))
                {
                    cmd.CommandTimeout = CommandTimeoutSeconds;
                    cmd.Parameters.AddWithValue("@val", mode.ToString());
                    cmd.ExecuteNonQuery();
                }

                // Persist each module's visibility
                foreach (var m in modules)
                {
                    using var cmd = new MySqlCommand(
                        @"INSERT INTO ModuleVisibility (ModuleName, IsEnabled)
                          VALUES (@name, @enabled)
                          ON DUPLICATE KEY UPDATE IsEnabled = @enabled;",
                        conn, tx);
                    cmd.CommandTimeout = CommandTimeoutSeconds;
                    cmd.Parameters.AddWithValue("@name", m.ModuleName);
                    cmd.Parameters.AddWithValue("@enabled", m.IsEnabled ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();

                // Update in-memory cache only after successful commit
                CurrentMode = mode;
                var newVisibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in modules)
                    newVisibility[m.ModuleName] = m.IsEnabled;
                EnsureDefaultModulesInCache(newVisibility);
                _visibility = newVisibility;

                NotifyChanged();
            }
            catch (Exception)
            {
                tx.Rollback();
                throw; // Let SettingsViewModel catch and display error message
            }
        }

        // ----------------------------------------------------------------
        // NotifyChanged – raise SettingsChanged event
        // ----------------------------------------------------------------
        /// <summary>
        /// Raises the SettingsChanged event so that subscribers such as
        /// SidebarViewModel can refresh their MenuGroups.
        /// </summary>
        internal void NotifyChanged()
        {
            SettingsChanged?.Invoke();
        }

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------
        private static void ExecuteNonQuery(MySqlConnection conn, string sql)
        {
            using var cmd = new MySqlCommand(sql, conn);
            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.ExecuteNonQuery();
        }

        private static SystemMode ParseSystemMode(string? raw)
        {
            if (Enum.TryParse<SystemMode>(raw, ignoreCase: true, out var parsed))
                return parsed;
            return SystemMode.RestaurantMode;
        }

        private static readonly string[] AllDefaultModules =
        {
            "Attendance", "Dashboard", "Employees", "Inventory",
            "Menu", "Orders", "Payroll", "PayslipRequests",
            "Receipts", "Sales", "Tables", "Users", "Settings"
        };

        private static Dictionary<string, bool> BuildDefaultVisibility()
        {
            var d = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in AllDefaultModules)
                d[m] = true;
            return d;
        }

        private static void EnsureDefaultModulesInCache(Dictionary<string, bool> visibility)
        {
            foreach (var m in AllDefaultModules)
            {
                if (!visibility.ContainsKey(m))
                    visibility[m] = true;
            }
        }
    }
}
