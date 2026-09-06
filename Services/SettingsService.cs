#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Singleton service that persists and retrieves SystemMode, ModuleVisibility,
    /// and BarcodeScanAction from the SQLite database. Schema is created by
    /// <see cref="AuthSchemaInitializer.EnsureSchemaAtStartup"/>.
    ///
    /// Call <see cref="EnsureSchemaAtStartup"/> once from App.xaml.cs (no-op kept
    /// for API compatibility), then call <see cref="Instance"/>.Load().
    /// </summary>
    public class SettingsService
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static readonly SettingsService Instance = new SettingsService();
        private SettingsService() { }

        // ── In-memory state ───────────────────────────────────────────────────
        public SystemMode        CurrentMode       { get; private set; } = SystemMode.StoreMode;
        public BarcodeScanAction BarcodeScanAction { get; private set; } = BarcodeScanAction.ModalPrompt;
        public string            StoreName         { get; private set; } = "SihyuPOS";

        private Dictionary<string, bool> _visibility =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, bool> ModuleVisibility => _visibility;

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>
        /// Raised after a successful <see cref="Save"/>; subscribers (e.g. SidebarViewModel)
        /// should call InitializeMenuItems() to refresh their MenuGroups.
        /// </summary>
        public event Action? SettingsChanged;

        // ── Schema (no-op — handled by AuthSchemaInitializer) ─────────────────
        public static void EnsureSchemaAtStartup()
        {
            Debug.WriteLine("[SettingsService] EnsureSchemaAtStartup — delegated to AuthSchemaInitializer.");
        }

        // ── Load ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Reads SystemMode, BarcodeScanAction and ModuleVisibility from SQLite.
        /// Falls back to defaults on any DB error.
        /// </summary>
        public void Load()
        {
            try
            {
                using var conn = SqliteConnectionFactory.CreateOpenConnection();

                // Read SystemMode
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT SettingValue FROM SystemSettings WHERE SettingKey = 'SystemMode' LIMIT 1;";
                    CurrentMode = ParseSystemMode(cmd.ExecuteScalar() as string);
                }

                // Read BarcodeScanAction
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT SettingValue FROM SystemSettings WHERE SettingKey = 'BarcodeScanAction' LIMIT 1;";
                    BarcodeScanAction = ParseBarcodeScanAction(cmd.ExecuteScalar() as string);
                }

                // Read StoreName
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT SettingValue FROM SystemSettings WHERE SettingKey = 'StoreName' LIMIT 1;";
                    var raw = cmd.ExecuteScalar() as string;
                    if (!string.IsNullOrWhiteSpace(raw))
                        StoreName = raw;
                }

                // Read ModuleVisibility
                var visibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ModuleName, IsEnabled FROM ModuleVisibility;";
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                        visibility[rdr.GetString(0)] = rdr.GetInt32(1) == 1;
                }

                EnsureDefaultModulesInCache(visibility);
                _visibility = visibility;

                Debug.WriteLine(
                    $"[SettingsService] Loaded. Mode={CurrentMode}, " +
                    $"BarcodeAction={BarcodeScanAction}, Modules={_visibility.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Load error — using defaults: {ex.Message}");
                CurrentMode       = SystemMode.StoreMode;
                BarcodeScanAction = BarcodeScanAction.ModalPrompt;
                _visibility       = BuildDefaultVisibility();
            }
        }

        // overload kept for API compat
        public void Load(string _) => Load();

        // ── Save ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Persists mode, barcode action, and module states in a single transaction.
        /// Updates in-memory cache and raises <see cref="SettingsChanged"/>.
        /// </summary>
        public void Save(SystemMode mode, IEnumerable<ModuleConfig> modules, BarcodeScanAction barcodeAction)
        {
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();

            try
            {
                UpsertSetting(conn, tx, "SystemMode",       mode.ToString());
                UpsertSetting(conn, tx, "BarcodeScanAction", barcodeAction.ToString());

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO ModuleVisibility (ModuleName, IsEnabled)
                        VALUES (@name, @enabled)
                        ON CONFLICT(ModuleName) DO UPDATE SET IsEnabled = excluded.IsEnabled;";
                    var pName    = cmd.Parameters.Add("@name",    SqliteType.Text);
                    var pEnabled = cmd.Parameters.Add("@enabled", SqliteType.Integer);
                    foreach (var m in modules)
                    {
                        pName.Value    = m.ModuleName;
                        pEnabled.Value = m.IsEnabled ? 1 : 0;
                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();

                CurrentMode       = mode;
                BarcodeScanAction = barcodeAction;
                var newVis = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in modules)
                    newVis[m.ModuleName] = m.IsEnabled;
                EnsureDefaultModulesInCache(newVis);
                _visibility = newVis;

                NotifyChanged();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // Convenience overload (barcode action defaults to current value)
        public void Save(SystemMode mode, IEnumerable<ModuleConfig> modules)
            => Save(mode, modules, BarcodeScanAction);

        // Legacy overload kept for any callers that pass a connection string (ignored)
        public void Save(SystemMode mode, IEnumerable<ModuleConfig> modules, string _)
            => Save(mode, modules);

        // ── SaveStoreName ─────────────────────────────────────────────────────
        /// <summary>Persists only the store name without touching mode or modules.</summary>
        public void SaveStoreName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                UpsertSetting(conn, tx, "StoreName", name);
                tx.Commit();
                StoreName = name;
                NotifyChanged();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ── NotifyChanged ─────────────────────────────────────────────────────
        internal void NotifyChanged() => SettingsChanged?.Invoke();

        // ── Private helpers ───────────────────────────────────────────────────
        private static void UpsertSetting(SqliteConnection conn, SqliteTransaction tx, string key, string value)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO SystemSettings (SettingKey, SettingValue)
                VALUES (@key, @val)
                ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = excluded.SettingValue;";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@val", value);
            cmd.ExecuteNonQuery();
        }

        private static SystemMode ParseSystemMode(string? raw)
        {
            if (Enum.TryParse<SystemMode>(raw, ignoreCase: true, out var parsed))
                return parsed;
            return SystemMode.StoreMode;
        }

        private static BarcodeScanAction ParseBarcodeScanAction(string? raw)
        {
            if (Enum.TryParse<BarcodeScanAction>(raw, ignoreCase: true, out var parsed))
                return parsed;
            return BarcodeScanAction.ModalPrompt;
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
            foreach (var m in AllDefaultModules) d[m] = true;
            return d;
        }

        private static void EnsureDefaultModulesInCache(Dictionary<string, bool> vis)
        {
            foreach (var m in AllDefaultModules)
                if (!vis.ContainsKey(m)) vis[m] = true;
        }
    }
}
